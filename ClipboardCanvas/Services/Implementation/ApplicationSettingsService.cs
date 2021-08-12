﻿using System.IO;
using Windows.Storage;
using ClipboardCanvas.Serialization;

namespace ClipboardCanvas.Services
{
    public class ApplicationSettingsService : BaseJsonSettingsModel, IApplicationSettingsService
    {
        #region Constructor

        public ApplicationSettingsService()
            : base (Path.Combine(ApplicationData.Current.LocalFolder.Path, Constants.LocalSettings.SETTINGS_FOLDERNAME, Constants.LocalSettings.APPLICATION_SETTINGS_FILENAME),
                  isCachingEnabled: true)
        {
        }

        #endregion

        #region IApplicationSettings

        public string LastVersionNumber
        {
            get => Get<string>(null);
            set => Set(value);
        }

        #endregion
    }
}