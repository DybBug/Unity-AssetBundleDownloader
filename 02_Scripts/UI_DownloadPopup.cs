using AssetBundleDownloader;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class UI_DownloadPopup : MonoBehaviour
{
    public enum PopupState
    {
        None = -1,
        CalculateDownloadSize,
        WaitingForDownload,
        WaitingForGamePlay,
        Downloading,
    }
    private enum SizeUnits
    {
        Byte, KB, MB, GB
    }

    [Serializable]
    public class SubGameObject
    {
        public PopupState State;
        public GameObject GameObject;
    }

    [SerializeField] private AssetDownloaderAsync m_Downloader;

    [Header("-- Header --")]
    [SerializeField] private TMP_Text m_TitleText;

    [Header("-- Body --")]
    [SerializeField] private TMP_Text m_DescText;

    [Header("-- Footer --")]
    [SerializeField] private List<SubGameObject> m_FooterObjects;
    [SerializeField] private TMP_Text m_DownloadingCapacity;
    [SerializeField] private Slider m_DownloadingProgress;
    [SerializeField] private Button m_DownloadButton;
    [SerializeField] private Button m_CancelButton;
    [SerializeField] private Button m_GamePlayButton;


    private PopupState m_CurrState;
    private DownloadStatus m_DownloadStatus;

    /// <summary> �ٿ�ε� ������ ���� </summary>
    private SizeUnits sizeUnit;

    /// <summary> ���� ��ȯ�� ���� �ٿ�ε�� ������ </summary>
    private long curDownloadedSizeInUnit;

    /// <summary> ���� ��ȯ�� �� ������ </summary>
    private long totalSizeInUnit;

    private void Awake()
    {
        m_Downloader.CatalogUpdatedEvent += OnCatalogUpdated;
        m_Downloader.SizeDownloadedEvent += OnSizeDownloaded;
        m_Downloader.DownloadFinishedEvent += OnDownloadFinished;
        m_Downloader.ExceptionEvent += OnException;

        m_DownloadButton.onClick.AddListener(OnClickStartDownload);
        m_CancelButton.onClick.AddListener(OnClickCancelBtn);
        m_GamePlayButton.onClick.AddListener(OnClickEnterGame);

    }

    void Start()
    {
        ChangeState(PopupState.None);
        m_Downloader.StartInitialize();
    }


    private void ClearText()
    {
        m_DescText.text = "";
    }

    private void SetText(string newText)
    {
        var text = m_DescText.text;
        text += $"\n{newText}";
        m_DescText.text = text;
    }




    // Update is called once per frame
    void Update()
    {
        if (m_Downloader.CurrentState == AssetBundleDownloaderState.Downloading)
        {
            m_DownloadStatus = m_Downloader.GetDownloadingStatus();
            RefreshUI();    
            curDownloadedSizeInUnit = ConvertByteByUnit(m_DownloadStatus.DownloadedBytes, sizeUnit);
        }
    }

    private void ChangeState(PopupState newState)
    { 
        var prevGameObject = m_FooterObjects.Find(e => e.State == m_CurrState);
        if (prevGameObject != null)
        {
            prevGameObject.GameObject.SetActive(false);
        }

        var currGameObject = m_FooterObjects.Find(e => e.State == newState);
        if (currGameObject != null)
        {
            currGameObject.GameObject.SetActive(true);
        }

        m_CurrState = newState;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (m_CurrState == PopupState.CalculateDownloadSize)
        {
            m_TitleText.text = "�˸�";
            SetText("�ٿ�ε� ������ �������� �ֽ��ϴ�. ��ø� ��ٷ��ּ���.");
        }
        else if (m_CurrState == PopupState.WaitingForDownload)
        {
            m_TitleText.text = "����";
            SetText($"�ٿ�ε带 �����ðڽ��ϱ� ? �����Ͱ� ���� ���� �� �ֽ��ϴ�. <color=green>({$"{this.totalSizeInUnit}{this.sizeUnit})</color>"}");
        }
        else if (m_CurrState == PopupState.Downloading)
        {
            m_TitleText.text = "�ٿ�ε���";

            double rate = m_DownloadStatus.TotalBytes == 0 ? 0 : (double)m_DownloadStatus.DownloadedBytes / (double)m_DownloadStatus.TotalBytes;
            ClearText();
            SetText($"�ٿ�ε����Դϴ�. ��ø� ��ٷ��ּ���. {(rate * 100).ToString("0.00")}% �Ϸ�");

            m_DownloadingProgress.value = (float)rate;
            m_DownloadingCapacity.text = $"{this.curDownloadedSizeInUnit}/{this.totalSizeInUnit}{sizeUnit}";
        }
        else if (m_CurrState == PopupState.WaitingForGamePlay)
        {
            m_TitleText.text = "�Ϸ�";
            SetText("������ �����Ͻðڽ��ϱ�?");
        }
    }

    public void OnClickStartDownload()
    {
        m_Downloader.StartDownload();
        ChangeState(PopupState.Downloading);
    }

    /// <summary> ��� ��ư Ŭ���� ȣ�� </summary>
    public void OnClickCancelBtn()
    {
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
#else
            Application.Quit();
#endif
    }

    /// <summary> �ΰ��� ���� ��ư Ŭ���� ȣ�� </summary>
    public void OnClickEnterGame()
    {
        Debug.Log("Start Game!");

        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }

    private void OnCatalogUpdateNotFound()
    {
        SetText("������Ʈ ������ īŻ�α׸� ã�� �� �����ϴ�.");
    }

    /// <summary> īŻ�α� ������Ʈ �Ϸ�� ȣ�� </summary>
    private void OnCatalogUpdated()
    {
        ChangeState(PopupState.CalculateDownloadSize);
    }

    /// <summary> ������ �ٿ�ε� �Ϸ�� ȣ�� </summary>
    private void OnSizeDownloaded(long size)
    {
        SetText($"�ٿ�ε� ������ : {size} ����Ʈ");

        if (size <= 0)
        {
            ChangeState(PopupState.WaitingForGamePlay);
        }
        else
        {
            sizeUnit = GetProperByteUnit(size);
            totalSizeInUnit = ConvertByteByUnit(size, sizeUnit);

            ChangeState(PopupState.WaitingForDownload);
        }
    }


    /// <summary> �ٿ�ε� �������� ȣ�� </summary>
    private void OnDownloadFinished()
    {
        SetText("�ٿ�ε� �Ϸ�!");
        ChangeState(PopupState.WaitingForGamePlay);
    }

    private void OnException(string message)
    {
        SetText(message);
    }

    private const long OneGB = 1000000000;
    private const long OneMB = 1000000;
    private const long OneKB = 1000;

    /// <summary> ����Ʈ <paramref name="byteSize"/> ����� �°Բ� ������ ���� <see cref="SizeUnits"/> Ÿ���� �����´� </summary>
    private SizeUnits GetProperByteUnit(long byteSize)
    {
        if (byteSize >= OneGB)
            return SizeUnits.GB;
        else if (byteSize >= OneMB)
            return SizeUnits.MB;
        else if (byteSize >= OneKB)
            return SizeUnits.KB;
        return SizeUnits.Byte;
    }

    /// <summary> ����Ʈ�� <paramref name="byteSize"/> <paramref name="unit"/> ������ �°� ���ڸ� ��ȯ�Ѵ� </summary>
    private long ConvertByteByUnit(long byteSize, SizeUnits unit)
    {
        return (long)((byteSize / (double)System.Math.Pow(1024, (long)unit)));
    }
}
