﻿using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.IO;
using Windows.Storage.Streams;
using System.Diagnostics;
using Windows.UI.Xaml.Media;
using System.Linq;
using System.Collections.Generic;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;

using ClipboardCanvas.Extensions;
using ClipboardCanvas.Helpers.SafetyHelpers;
using ClipboardCanvas.Helpers.SafetyHelpers.ExceptionReporters;
using ClipboardCanvas.EventArguments;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.Models;
using ClipboardCanvas.ModelViews;
using ClipboardCanvas.Enums;
using ClipboardCanvas.EventArguments.CanvasControl;
using ClipboardCanvas.Helpers.Filesystem;

namespace ClipboardCanvas.ViewModels.UserControls.CanvasDisplay
{
    public sealed class ImageCanvasViewModel : BasePasteCanvasViewModel, ICanvasContentDraggable
    {
        #region Private Members

        private readonly IDynamicPasteCanvasControlView _view;

        private Stream _dataStream;

        private SoftwareBitmap _softwareBitmap;

        #endregion

        #region Protected Properties

        protected override ICollectionsContainerModel AssociatedContainer => _view?.CollectionContainer;

        #endregion

        #region Public Properties

        public static List<string> Extensions => new List<string>() {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".ico", ".svg", ".webp"
        };

        private ImageSource _ContentImage;
        public ImageSource ContentImage
        {
            get => _ContentImage;
            set => SetProperty(ref _ContentImage, value);
        }

        private bool _IsDragAvailable;
        public bool IsDragAvailable
        {
            get => _IsDragAvailable;
            set => SetProperty(ref _IsDragAvailable, value);
        }

        #endregion

        #region Events

        public event EventHandler OnDragStartedEvent;

        #endregion

        #region Constructor

        public ImageCanvasViewModel(IDynamicPasteCanvasControlView view)
            : base(StaticExceptionReporters.DefaultSafeWrapperExceptionReporter) // TODO: Use custom exception reporter
        {
            this._view = view;
        }

        #endregion

        #region Public Helpers

        public IReadOnlyList<IStorageItem> ProvideDragData()
        {
            return new List<IStorageItem>() { sourceFile };
        }

        public void RaiseOnDragStartedEvent()
        {
            OnDragStartedEvent?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Override

        protected override async Task<SafeWrapperResult> SetData(DataPackageView dataPackage)
        {
            // Always won't contain StorageItems since they are handled in SetDataInternal()

            SafeWrapper<RandomAccessStreamReference> bitmap = await SafeWrapperRoutines.SafeWrapAsync(
                       () => dataPackage.GetBitmapAsync().AsTask());

            if (!bitmap)
            {
                Debugger.Break();
                return (SafeWrapperResult)bitmap;
            }

            SafeWrapper<IRandomAccessStreamWithContentType> openedStream = await SafeWrapperRoutines.SafeWrapAsync(
                () => bitmap.Result.OpenReadAsync().AsTask());

            if (!openedStream)
            {
                Debugger.Break();
                return (SafeWrapperResult)openedStream;
            }

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(openedStream.Result);

            _softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            return SafeWrapperResult.S_SUCCESS;
        }

        public override async Task<SafeWrapperResult> TrySaveData()
        {
            SafeWrapperResult result;

            BitmapEncoder encoder = null;
            result = await SafeWrapperRoutines.SafeWrapAsync(async () =>
            {
                using (IRandomAccessStream fileStream = await sourceFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);

                    encoder.SetSoftwareBitmap(_softwareBitmap);

                    await encoder.FlushAsync();
                }
            }, errorReporter);

            if (!result)
            {
                const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                int hresult = result.Details.innerException.HResult;

                result = SafeWrapperRoutines.SafeWrap(async () =>
                {
                    if (hresult == WINCODEC_ERR_UNSUPPORTEDOPERATION)
                    {
                        using (IRandomAccessStream fileStream = await sourceFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            encoder.IsThumbnailGenerated = false;

                            await encoder.FlushAsync();
                        }
                    }
                }, errorReporter);
            }

            if (result)
            {
                RaiseOnFileModifiedEvent(this, new FileModifiedEventArgs(sourceFile, AssociatedContainer));
            }

            return result;
        }

        protected override async Task<SafeWrapperResult> SetData(StorageFile file)
        {
            SafeWrapperResult result;

            SafeWrapper<Stream> openedStream = await SafeWrapperRoutines.SafeWrapAsync(
                    () => sourceFile.OpenStreamForReadAsync());

            if (!openedStream)
            {
                return (SafeWrapperResult)openedStream;
            }

            _dataStream = openedStream.Result;

            if (!openedStream)
            {
                return (SafeWrapperResult)openedStream;
            }

            result = await SafeWrapperRoutines.SafeWrapAsync(async () =>
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(_dataStream.AsRandomAccessStream());
                _softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            });

            return result;
        }

        protected override async Task<SafeWrapper<StorageFile>> TrySetFileWithExtension()
        {
            SafeWrapper<StorageFile> file = await AssociatedContainer.GetEmptyFileToWrite(".png");

            return file;
        }

        protected override async Task<SafeWrapperResult> TryFetchDataToView()
        {
            SafeWrapperResult result = null;

            result = await SafeWrapperRoutines.SafeWrapAsync(async () =>
            {
                if (_dataStream == null) // Data is pasted, load from _softwareBitmap
                {
                    byte[] buffer = await ImagingHelpers.GetBytesFromSoftwareBitmap(_softwareBitmap, BitmapEncoder.PngEncoderId);
                    ContentImage = await ImagingHelpers.ToBitmapAsync(buffer);
                    Array.Clear(buffer, 0, buffer.Length);
                }
                else // Data is read from file, load from data stream
                {
                    BitmapImage image = new BitmapImage();
                    ContentImage = image;
                    _dataStream.Position = 0;
                    await image.SetSourceAsync(_dataStream.AsRandomAccessStream());
                }
            });

            return result;
        }

        #endregion

        #region IDisposable

        public override void Dispose()
        {
            base.Dispose();

            _dataStream?.Dispose();
            _softwareBitmap?.Dispose();

            _softwareBitmap = null;
            _dataStream = null;
            _ContentImage = null;
        }

        #endregion
    }
}
