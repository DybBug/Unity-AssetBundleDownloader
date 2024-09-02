using AssetBundleDownloader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;


public class AssetDownloaderEvent : MonoBehaviour
{
    [SerializeField]
    private AddressableLabels m_AddressableLabels;

    #region Events
    public event Action CatalogUpdatedEvent;        // 카탈로그가 업데이트 되었을때 발생
    public event Action<long/*totalSize*/> SizeDownloadedEvent; // 다운로드 용량을 가져왔을떄 발생
    public event Action DownloadFinishedEvent;
    public event Action<string> ExceptionEvent;
    #endregion

    private List<string> m_Catalogs;
    private DownloadStatus m_DownloadStatus;
    private readonly Dictionary<AsyncOperationHandle<long>, long> m_DownloadSizesByHandle = new(); // default value = -1
    private readonly Dictionary<AsyncOperationHandle, bool> m_DownloadResultByHandle = new(); // default value = false

    public AssetBundleDownloaderState CurrentState { get; private set; } = AssetBundleDownloaderState.WaitingForInitialize;

    public void StartInitialize()
    {
        Debug.Assert(CurrentState == AssetBundleDownloaderState.WaitingForInitialize);
        ExecuteByWaitingState();
    }

    public void StartDownload()
    {
        Debug.Assert(CurrentState == AssetBundleDownloaderState.WaitingForDownload);

        m_DownloadStatus.TotalBytes = 0;
        m_DownloadStatus.DownloadedBytes = 0;
        m_DownloadStatus.IsDone = false;

        ExecuteByWaitingState();
    }

    public DownloadStatus GetDownloadingStatus()
    {
        var isDone = true;
        foreach (var downloadHandle in m_DownloadResultByHandle.Keys)
        {
            if (!downloadHandle.IsValid() || downloadHandle.IsDone || downloadHandle.Status == AsyncOperationStatus.Failed)
                continue;

            var status = downloadHandle.GetDownloadStatus();

            isDone &= status.IsDone;
            m_DownloadStatus.TotalBytes += status.TotalBytes;
            m_DownloadStatus.DownloadedBytes += status.DownloadedBytes;
        }
        m_DownloadStatus.IsDone = isDone;

        return m_DownloadStatus;
    }

    private void ExecuteByWaitingState()
    {
        try
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                throw new Exception("network is NotReachable");
            }

            switch (CurrentState)
            {
                case AssetBundleDownloaderState.WaitingForInitialize:
                {
                    Initialize();
                    break;
                }
                case AssetBundleDownloaderState.WaitingForCatalogCheck:
                {
                    CheckCatalog();
                    break;
                }
                case AssetBundleDownloaderState.WaitingForCatalogUpdate:
                {
                    UpdateCatalog();
                    break;
                }
                case AssetBundleDownloaderState.WaitingForSizeDownload:
                {
                    DownloadSize();
                    break;
                }
                case AssetBundleDownloaderState.WaitingForDownload:
                {
                    Download();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ExceptionEvent?.Invoke($"{ex.Message}\n[Call Stack]\n{ex.StackTrace}");
        }
    }

    private void Initialize()
    {
        CurrentState = AssetBundleDownloaderState.Initializing;
        Addressables.InitializeAsync().Completed += OnInitialized;          
    }

    private void OnInitialized(AsyncOperationHandle<IResourceLocator> handle)
    {
        CurrentState = AssetBundleDownloaderState.WaitingForCatalogCheck;
        ExecuteByWaitingState();
    }

    private void CheckCatalog()
    {
        CurrentState = AssetBundleDownloaderState.CatalogChecking;
        Addressables.CheckForCatalogUpdates().Completed += OnCheckedCatalogWithNotify;
    }

    private void OnCheckedCatalogWithNotify(AsyncOperationHandle<List<string>> handle)
    {
        var catalogs = handle.Result;
        if (catalogs.Count > 0)
        {
            m_Catalogs = catalogs;
            CurrentState = AssetBundleDownloaderState.WaitingForCatalogUpdate;
        }
        else
        {
            CatalogUpdatedEvent?.Invoke();
            CurrentState = AssetBundleDownloaderState.WaitingForSizeDownload;
        }
        ExecuteByWaitingState();
    }

    private void UpdateCatalog()
    {
        CurrentState = AssetBundleDownloaderState.CatalogUpdating;
        Addressables.UpdateCatalogs(m_Catalogs).Completed += OnUpdatedCatalogWithNotify;
    }

    private void OnUpdatedCatalogWithNotify(AsyncOperationHandle<List<IResourceLocator>> handle)
    {
        CatalogUpdatedEvent?.Invoke();
        CurrentState = AssetBundleDownloaderState.WaitingForSizeDownload;
        m_Catalogs.Clear();
    }

    private void DownloadSize()
    {
        CurrentState = AssetBundleDownloaderState.SizeDownloading;
        foreach (var assetLabelReference in m_AddressableLabels.AssetLabelReferences)
        {
            var handle = Addressables.GetDownloadSizeAsync(assetLabelReference.labelString);
            handle.Completed += OnDownloadedSizeWithNotify;
            m_DownloadSizesByHandle.Add(handle, -1);
        }
    }

    private void OnDownloadedSizeWithNotify(AsyncOperationHandle<long> handle)
    {
        m_DownloadSizesByHandle[handle] = handle.Result;
        if (m_DownloadSizesByHandle.All(e => e.Value != -1))
        {
            var totalSize = 0L;
            foreach(var pair in m_DownloadSizesByHandle)
            {
                totalSize += pair.Value;
            }
            m_DownloadSizesByHandle.Clear();

            SizeDownloadedEvent?.Invoke(totalSize);
            CurrentState = AssetBundleDownloaderState.WaitingForDownload;
        }
    }

    private void Download()
    {
        CurrentState = AssetBundleDownloaderState.Downloading;
        foreach (var assetLabelReference in m_AddressableLabels.AssetLabelReferences)
        {
            var handle = Addressables.DownloadDependenciesAsync(assetLabelReference.labelString);
            handle.Completed += CompletedDownloadWithNotify;
            m_DownloadResultByHandle.Add(handle, false);
        }
    }

    private void CompletedDownloadWithNotify(AsyncOperationHandle handle)
    {
        m_DownloadResultByHandle[handle] = true;
        if (m_DownloadResultByHandle.All(e => e.Value == true))
        {
            m_DownloadResultByHandle.Clear();

            DownloadFinishedEvent?.Invoke();
            CurrentState = AssetBundleDownloaderState.FinishDownload;
        }
    }
}
