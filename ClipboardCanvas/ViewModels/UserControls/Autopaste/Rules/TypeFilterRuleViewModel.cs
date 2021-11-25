﻿using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Newtonsoft.Json;

using ClipboardCanvas.Models.Autopaste;
using ClipboardCanvas.Helpers;
using ClipboardCanvas.Helpers.SafetyHelpers;

namespace ClipboardCanvas.ViewModels.UserControls.Autopaste.Rules
{
    internal class TypeFilterRuleViewModel : BaseAutopasteRuleViewModel
    {
        [JsonIgnore]
        private int _SelectedIndex;
        public int SelectedIndex
        {
            get => _SelectedIndex;
            set => SetProperty(ref _SelectedIndex, value);
        }

        public TypeFilterRuleViewModel(IRuleActions ruleActions)
            : base(ruleActions)
        {
            ruleName = "Type filter";
        }

        public override async Task<bool> PassesRule(DataPackageView dataPackage)
        {
            switch (_SelectedIndex)
            {
                case 0: // Image
                    {
                        if (dataPackage.ContainsOnly(StandardDataFormats.Bitmap))
                        {
                            return false;
                        }

                        break;
                    }

                case 1: // Text
                    {
                        if (dataPackage.ContainsOnly(StandardDataFormats.Text))
                        {
                            SafeWrapper<string> result = await dataPackage.SafeGetTextAsync();

                            if (result && !await WebHelpers.IsValidUrl(result))
                            {
                                return false;
                            }
                        }

                        break;
                    }

                case 2: // File
                    {
                        if (dataPackage.ContainsOnly(StandardDataFormats.StorageItems))
                        {
                            return false;
                        }

                        break;
                    }

                case 3: // Url
                    {
                        if (dataPackage.ContainsOnly(StandardDataFormats.Text))
                        {
                            SafeWrapper<string> result = await dataPackage.SafeGetTextAsync();

                            if (result && await WebHelpers.IsValidUrl(result))
                            {
                                return false;
                            }
                        }

                        break;
                    }

                default:
                    return false;
            }

            return true;
        }
    }
}
