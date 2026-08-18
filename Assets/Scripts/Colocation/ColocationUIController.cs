using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class ColocationUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private SharedAnchorHost sharedAnchorHost;
        [SerializeField] private SharedAnchorClient sharedAnchorClient;
        [SerializeField] private SharedSpaceAligner sharedSpaceAligner;

        [Header("UI")]
        [SerializeField] private TMP_InputField sessionNameInput;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private int maxStatusLines = 12;

        [Header("Auto Mode")]
        [SerializeField] private bool autoStartOnEnable = true;
        [SerializeField] private bool autoHostIfNoSession = true;
        [SerializeField] private float autoStartDelay = 0.5f;

        private readonly StringBuilder statusBuffer = new StringBuilder();
        private bool autoStarted;

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindObjectOfType<SessionManager>();
            }

            if (sharedAnchorHost == null)
            {
                sharedAnchorHost = FindObjectOfType<SharedAnchorHost>();
            }

            if (sharedAnchorClient == null)
            {
                sharedAnchorClient = FindObjectOfType<SharedAnchorClient>();
            }

            if (sharedSpaceAligner == null)
            {
                sharedSpaceAligner = FindObjectOfType<SharedSpaceAligner>();
            }

            if (sessionNameInput != null && string.IsNullOrWhiteSpace(sessionNameInput.text))
            {
                sessionNameInput.text = GenerateDefaultSessionName();
            }

            if (statusText == null)
            {
                var statusObject = GameObject.Find("StatusText");
                if (statusObject != null)
                {
                    statusText = statusObject.GetComponent<TMP_Text>();
                }
            }
        }

        private void OnEnable()
        {
            if (sessionManager != null)
            {
                sessionManager.StatusMessage += AppendStatus;
                sessionManager.SharedAnchorIdChanged += OnAnchorIdChanged;
                sessionManager.AutoHostRequested += HandleAutoHostRequested;
            }

            if (sharedAnchorHost != null)
            {
                sharedAnchorHost.StatusMessage += AppendStatus;
            }

            if (sharedAnchorClient != null)
            {
                sharedAnchorClient.StatusMessage += AppendStatus;
            }
        }

        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.StatusMessage -= AppendStatus;
                sessionManager.SharedAnchorIdChanged -= OnAnchorIdChanged;
                sessionManager.AutoHostRequested -= HandleAutoHostRequested;
            }

            if (sharedAnchorHost != null)
            {
                sharedAnchorHost.StatusMessage -= AppendStatus;
            }

            if (sharedAnchorClient != null)
            {
                sharedAnchorClient.StatusMessage -= AppendStatus;
            }
        }

        public void OnHostClicked()
        {
            if (sharedAnchorHost == null)
            {
                AppendStatus("SharedAnchorHost missing.");
                return;
            }

            sharedAnchorHost.BeginPlacement(GetSessionName());
        }

        public void OnJoinClicked()
        {
            if (sessionManager == null)
            {
                AppendStatus("SessionManager missing.");
                return;
            }

            sessionManager.StartClient(GetSessionName());
        }

        public void OnReAlignClicked()
        {
            if (sharedSpaceAligner != null)
            {
                sharedSpaceAligner.ReAlign();
            }
        }

        private void Start()
        {
            if (autoStartOnEnable && !autoStarted)
            {
                autoStarted = true;
                StartCoroutine(AutoStartRoutine());
            }
        }

        private IEnumerator AutoStartRoutine()
        {
            if (autoStartDelay > 0f)
            {
                yield return new WaitForSeconds(autoStartDelay);
            }

            if (sessionManager == null)
            {
                AppendStatus("SessionManager missing. Auto start canceled.");
                yield break;
            }

            sessionManager.StartAuto(GetSessionName());
        }

        private void OnAnchorIdChanged(string anchorId)
        {
            AppendStatus($"Anchor ID received: {anchorId}");
        }

        private void HandleAutoHostRequested(string sessionName)
        {
            if (!autoHostIfNoSession)
            {
                AppendStatus("Auto host disabled.");
                return;
            }

            if (sharedAnchorHost == null)
            {
                AppendStatus("SharedAnchorHost missing. Starting Host without anchor placement.");
                if (sessionManager != null)
                {
                    sessionManager.StartHost(sessionName);
                }

                return;
            }

            sharedAnchorHost.BeginPlacement(sessionName);
        }

        private string GetSessionName()
        {
            if (sessionNameInput != null && !string.IsNullOrWhiteSpace(sessionNameInput.text))
            {
                return sessionNameInput.text.Trim();
            }

            return sessionManager != null ? sessionManager.DefaultSessionName : "MRRoom_";
        }

        private string GenerateDefaultSessionName()
        {
            var baseName = sessionManager != null ? sessionManager.DefaultSessionName : "MRRoom_";
            var code = Random.Range(1000, 9999);
            return $"{baseName}{code}";
        }

        private void AppendStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusBuffer.AppendLine(message);

            var lines = statusBuffer.ToString().Split('\n');
            if (lines.Length > maxStatusLines)
            {
                statusBuffer.Clear();
                for (int i = lines.Length - maxStatusLines; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        statusBuffer.AppendLine(lines[i]);
                    }
                }
            }

            statusText.text = statusBuffer.ToString();
        }
    }
}
