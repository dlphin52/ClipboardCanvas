﻿using System;
using Windows.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.Foundation;
using System.Collections.Generic;
using Windows.Storage;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using System.Numerics;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

using ClipboardCanvas.ViewModels.UserControls;
using ClipboardCanvas.ModelViews;
using ClipboardCanvas.Models;
using ClipboardCanvas.Extensions;
using ClipboardCanvas.Helpers.Filesystem;
using ClipboardCanvas.DataModels;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace ClipboardCanvas.UserControls
{
    public sealed partial class InteractableCanvasControl : UserControl, IInteractableCanvasControlView
    {
        private Canvas _canvasPanel;

        private Point _savedClickPosition;

        public InteractableCanvasControlViewModel ViewModel
        {
            get => (InteractableCanvasControlViewModel)DataContext;
            set => DataContext = value;
        }

        public InteractableCanvasControl()
        {
            this.InitializeComponent();

            this.ViewModel = new InteractableCanvasControlViewModel(this);
        }

        private async void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            _canvasPanel = sender as Canvas;
            await this.ViewModel.CanvasLoaded();
        }

        private void Canvas_Drop(object sender, DragEventArgs e)
        {
            Point dropPoint = e.GetPosition(_canvasPanel);

            FrameworkElement element = e.DataView.Properties[Constants.UI.CanvasContent.INFINITE_CANVAS_DRAGGED_OBJECT_ID] as FrameworkElement;
            if (element?.DataContext == null && e.DataView != null)
            {
                // Save position and dataPackage
                this.ViewModel.DataPackageComparisionDataModel = new InteractableCanvasDataPackageComparisionDataModel(e.DataView, dropPoint);
            }
            else
            {
                int indexOfItem = this.ViewModel.Items.IndexOf(element.DataContext as InteractableCanvasControlItemViewModel);
                UIElement container = ItemsHolder.ContainerFromIndex(indexOfItem) as UIElement;

                Canvas.SetLeft(container, dropPoint.X - _savedClickPosition.X);
                Canvas.SetTop(container, dropPoint.Y - _savedClickPosition.Y);

                element.Opacity = 1.0d;

                // Update the ZIndex of the element - set it on top
                SetOnTop(container);

                this.ViewModel.ItemRearranged();
            }
        }

        private void SetOnTop(UIElement element)
        {
            if (element == null)
            {
                return;
            }

            int elementZIndex = Canvas.GetZIndex(element);
            int highestZIndex = Math.Max(elementZIndex, 1);

            foreach (object item in ItemsHolder.Items)
            {
                UIElement uiItemContainer = ItemsHolder.ContainerFromItem(item) as UIElement;

                int itemZIndex = Canvas.GetZIndex(uiItemContainer);
                highestZIndex = Math.Max(itemZIndex, highestZIndex);

                if (itemZIndex > elementZIndex)
                {
                    Canvas.SetZIndex(uiItemContainer, itemZIndex - 1);
                }
            }

            // Set the highest ZIndex
            Canvas.SetZIndex(element, highestZIndex);
        }

        private void Canvas_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;

            // Hide element when dragging over the canvas
            if (e.DataView.Properties[Constants.UI.CanvasContent.INFINITE_CANVAS_DRAGGED_OBJECT_ID] is FrameworkElement element 
                && this.ViewModel.Items.Contains(element.DataContext as InteractableCanvasControlItemViewModel))
            {
                element.Opacity = 0.0d;
            }
        }

        private async void RootContentGrid_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (sender is FrameworkElement draggedElement)
            {
                Point point = args.GetPosition(draggedElement);
                _savedClickPosition = point;

                // Add the dragged element to properties which we can later retrieve it from
                args.Data.Properties.Add(Constants.UI.CanvasContent.INFINITE_CANVAS_DRAGGED_OBJECT_ID, draggedElement);

                // Also set data associated from the dragged element
                if (draggedElement.DataContext is IDragDataProviderModel dragDataProvider)
                {
                    IReadOnlyList<IStorageItem> dragData = await dragDataProvider.GetDragData();

                    if (dragData.CheckNotNull())
                    {
                        args.Data.SetStorageItems(await dragDataProvider.GetDragData());
                    }
                }
            }
        }

        private void Canvas_DragLeave(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;

            if (e.DataView.Properties[Constants.UI.CanvasContent.INFINITE_CANVAS_DRAGGED_OBJECT_ID] is FrameworkElement draggedElement)
            {
                draggedElement.Opacity = 1.0d;
            }
        }

        private async void RootContentGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is InteractableCanvasControlItemViewModel itemViewModel)
            {
                await StorageHelpers.OpenFile(await itemViewModel.CanvasItem.SourceItem);
            }
        }

        public Vector2 GetItemPosition(InteractableCanvasControlItemViewModel itemViewModel)
        {
            int indexOfItem = this.ViewModel.Items.IndexOf(itemViewModel);

            if (ItemsHolder.ContainerFromIndex(indexOfItem) is UIElement container)
            {
                float x = (float)Canvas.GetLeft(container);
                float y = (float)Canvas.GetTop(container);

                return new Vector2(x, y);
            }

            return new Vector2(0, 0);
        }

        public void SetItemPosition(InteractableCanvasControlItemViewModel itemViewModel, Vector2 position)
        {
            int indexOfItem = this.ViewModel.Items.IndexOf(itemViewModel);

            if (ItemsHolder.ContainerFromIndex(indexOfItem) is UIElement container)
            {
                Canvas.SetLeft(container, (double)position.X);
                Canvas.SetTop(container, (double)position.Y);
            }
        }

        public async Task<IRandomAccessStream> GetCanvasImageStream()
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(ItemsHolder);

            IBuffer pixelsBuffer = await rtb.GetPixelsAsync();
            byte[] pixelArray = pixelsBuffer.ToArray();

            DisplayInformation displayInfo = DisplayInformation.GetForCurrentView();

            IRandomAccessStream stream = new InMemoryRandomAccessStream();
            BitmapEncoder bitmapEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            bitmapEncoder.SetPixelData(BitmapPixelFormat.Bgra8, // RGB with alpha
                                 BitmapAlphaMode.Premultiplied,
                                 (uint)rtb.PixelWidth,
                                 (uint)rtb.PixelHeight,
                                 displayInfo.RawDpiX,
                                 displayInfo.RawDpiY,
                                 pixelArray);

            await bitmapEncoder.FlushAsync();
            stream.Seek(0);

            Array.Clear(pixelArray, 0, pixelArray.Length);

            return stream;
        }

        public void SetOnTop(InteractableCanvasControlItemViewModel itemViewModel)
        {
            SetOnTop(ItemsHolder.ContainerFromItem(itemViewModel) as UIElement);
        }

        public int GetCanvasTopIndex(InteractableCanvasControlItemViewModel itemViewModel)
        {
            if (ItemsHolder.ContainerFromItem(itemViewModel) is UIElement container)
            {
                return Canvas.GetZIndex(container);
            }

            return 0;
        }

        public void SetCanvasTopIndex(InteractableCanvasControlItemViewModel itemViewModel, int topIndex)
        {
            if (ItemsHolder.ContainerFromItem(itemViewModel) is UIElement container)
            {
                Canvas.SetZIndex(container, topIndex);
            }
        }
    }
}
