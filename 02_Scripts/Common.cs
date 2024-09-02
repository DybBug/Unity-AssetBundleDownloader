using UnityEditor;
using UnityEngine;

namespace AssetBundleDownloader
{
    public enum AssetBundleDownloaderState
    {
        WaitingForInitialize,
        Initializing,

        WaitingForCatalogCheck,
        CatalogChecking,

        WaitingForCatalogUpdate,
        CatalogUpdating,

        WaitingForSizeDownload,
        SizeDownloading,

        WaitingForDownload,
        Downloading,

        FinishDownload
    }
}