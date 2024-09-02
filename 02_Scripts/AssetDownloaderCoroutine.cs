using AssetBundleDownloader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;



public class AssetDownloaderCoroutine : MonoBehaviour
{
    [SerializeField]
    private AddressableLabels m_AddressableLabels;

    #region Events
    public event Action CatalogUpdatedEvent;        // 카탈로그가 업데이트 되었을때 발생
    public event Action<long/*totalSize*/> SizeDownloadedEvent; // 다운로드 용량을 가져왔을떄 발생
    public event Action DownloadFinishedEvent;
    public event Action<string> ExceptionEvent;
    #endregion

    private DownloadStatus m_DownloadStatus;
    public AssetBundleDownloaderState CurrentState { get; private set; } = AssetBundleDownloaderState.WaitingForInitialize;
    private readonly List<AsyncOperationHandle> m_DownloadHandles = new();


    public IEnumerator StartInitializeCoroutine()
    {
        Debug.Assert(CurrentState == AssetBundleDownloaderState.WaitingForInitialize);

        yield return InitializeCoroutine();
        yield return CheckAndUpdateCatalogCoroutine();
        yield return DownloadSizeCoroutine();
    }

    public IEnumerator StartDownloadCoroutine()
    {
        Debug.Assert(CurrentState == AssetBundleDownloaderState.WaitingForDownload);
        m_DownloadStatus.TotalBytes = 0;
        m_DownloadStatus.DownloadedBytes = 0;
        m_DownloadStatus.IsDone = false;

        yield return DownloadCoroutine();
    }

    public DownloadStatus GetDownloadingStatus()
    {
        var isDone = true;
        foreach (var downloadHandle in m_DownloadHandles)
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

    private IEnumerator InitializeCoroutine()
    {
        CurrentState = AssetBundleDownloaderState.Initializing;
        var handle = Addressables.InitializeAsync(false);
        if (!handle.IsValid())
        {
            Addressables.Release(handle);
            throw handle.OperationException;
        }
        yield return handle;

        if (!handle.IsDone || handle.Status == AsyncOperationStatus.Failed)
        {
            Addressables.Release(handle);
            throw handle.OperationException;
        }
        CurrentState = AssetBundleDownloaderState.WaitingForCatalogCheck;
        Addressables.Release(handle);
    }

    private IEnumerator CheckAndUpdateCatalogCoroutine()
    {
        var checkHandle = Addressables.CheckForCatalogUpdates(false);
        CurrentState = AssetBundleDownloaderState.CatalogChecking;
        if (!checkHandle.IsValid())
        {
            Addressables.Release(checkHandle);
            throw checkHandle.OperationException;
        }
        yield return checkHandle;
        if (!checkHandle.IsDone || checkHandle.Status == AsyncOperationStatus.Failed)
        {
            Addressables.Release(checkHandle);
            throw checkHandle.OperationException;
        }
        CurrentState = AssetBundleDownloaderState.WaitingForCatalogUpdate;

        var catalogs = checkHandle.Result;
        if (catalogs.Count <= 0)
        {
            Addressables.Release(checkHandle);
            CurrentState = AssetBundleDownloaderState.WaitingForSizeDownload;
            CatalogUpdatedEvent?.Invoke();
            yield break;
        }
        Addressables.Release(checkHandle);

        var updateHandle = Addressables.UpdateCatalogs(catalogs);
        CurrentState = AssetBundleDownloaderState.CatalogUpdating;
        if (!updateHandle.IsValid())
        {
            Addressables.Release(updateHandle);
            throw updateHandle.OperationException;
        }

        yield return updateHandle;
        if (!updateHandle.IsDone || updateHandle.Status == AsyncOperationStatus.Failed)
        {
            Addressables.Release(updateHandle);
            throw updateHandle.OperationException;
        }

        Addressables.Release(updateHandle);
        CurrentState = AssetBundleDownloaderState.WaitingForSizeDownload;
        CatalogUpdatedEvent?.Invoke();
    }

    private IEnumerator DownloadSizeCoroutine()
    {
        var tasksByHandle = new Dictionary<AsyncOperationHandle<long>, Task>();

        CurrentState = AssetBundleDownloaderState.SizeDownloading;
        foreach (var assetLabelReference in m_AddressableLabels.AssetLabelReferences)
        {
            var handle = Addressables.GetDownloadSizeAsync(assetLabelReference.labelString);
            tasksByHandle.Add(handle, handle.Task);
            if (!handle.IsValid())
            {
                foreach (var handleInDic in tasksByHandle)
                {
                    Addressables.Release(handleInDic);
                }
                tasksByHandle.Clear();
                throw handle.OperationException;
            }
        }

        // 모든 코루틴이 완료될 때까지 대기
        var totalSize = 0L;
        foreach (var handle in tasksByHandle.Keys)
        {
            yield return handle;
            totalSize += handle.Result;
            Addressables.Release(handle);
        }

        CurrentState = AssetBundleDownloaderState.WaitingForDownload;
        SizeDownloadedEvent?.Invoke(totalSize);
    }

    private IEnumerator DownloadCoroutine()
    {
        CurrentState = AssetBundleDownloaderState.Downloading;
        var tasksByHandle = new Dictionary<AsyncOperationHandle, Task>();
        foreach (var assetLabelReference in m_AddressableLabels.AssetLabelReferences)
        {
            var handle = Addressables.DownloadDependenciesAsync(assetLabelReference.labelString, false);
            m_DownloadHandles.Add(handle);
            tasksByHandle.Add(handle, handle.Task);

            if (!handle.IsValid())
            {
                for (var i = 0; i < tasksByHandle.Count; ++i)
                {
                    Addressables.Release(m_DownloadHandles[i]);
                }
                tasksByHandle.Clear();
                m_DownloadHandles.Clear();
                throw handle.OperationException;
            }     
        }

        // 모든 코루틴이 완료될 때까지 대기
        foreach (var handle in tasksByHandle.Keys)
        {
            yield return handle;
            m_DownloadHandles.Remove(handle);
            Addressables.Release(handle);
        }

        CurrentState = AssetBundleDownloaderState.FinishDownload;
        DownloadFinishedEvent?.Invoke();
    }
}
