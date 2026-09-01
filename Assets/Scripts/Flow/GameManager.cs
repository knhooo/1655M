using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Player;
using Game.Map;
using Game.CameraRig;
using Game.Economy;
using Game.Equipment;

namespace Game.Flow
{
    /// <summary>
    /// 게임 한 판의 전체 흐름을 관리하는 단일 상태 머신.
    ///
    ///   Title ──(Ready)──▶ Inventory ──(Dive)──▶ Playing ──(사망)──▶ Ending ──(씬 리로드)──▶ Title
    ///
    ///  - Title    : 타이틀 UI (제목 / Best / Ready)
    ///  - Inventory: 위에서 내려오는 인벤토리 UI (장비). Dive 로 출발
    ///  - Playing  : HUD 표시, 조작 활성화
    ///  - Ending   : 정지 → 카메라 되감기 → 이번/최고 심도 표시 → 페이드 아웃 → 같은 씬 리로드
    ///
    /// 항상 활성인 매니저 오브젝트에 붙일 것.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CameraFollow _camera;
        [SerializeField] private MapGenerator _map;
        [SerializeField] private ScreenFader _fader;

        private static PlayerController Player => PlayerController.Instance;

        [Header("UI")]
        [SerializeField] private TitleScreen _titleScreen;
        [SerializeField] private InventoryUI _inventoryUI;
        [Tooltip("인게임 HUD. 기본 비활성.")]
        [SerializeField] private GameObject _hud;
        [SerializeField] private ResultScreen _resultScreen;

        [Header("사망 연출")]
        [SerializeField] private float _deathPause = 0.4f;
        [SerializeField] private float _rewindSpeed = 16f;
        [SerializeField] private float _rewindMinDuration = 0.6f;
        [SerializeField] private float _rewindMaxDuration = 2.5f;
        [SerializeField] private float _resultHold = 2.5f;

        public enum State { Title, Inventory, Playing, Ending }
        public State Current { get; private set; }

        /// <summary>씬 단위 싱글톤. 씬 리로드마다 새로 생성된다 (DontDestroyOnLoad 아님).</summary>
        public static GameManager Instance { get; private set; }

        private const string BestDepthKey = "best_depth";
        private const string CurrencyKeyPrefix = "total_currency_";

        public static int BestDepth
        {
            get => PlayerPrefs.GetInt(BestDepthKey, 0);
            private set { PlayerPrefs.SetInt(BestDepthKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>아웃게임 누적 재화(종류별). 강화에서 소모.</summary>
        public static int GetTotalCurrency(CurrencyType type)
        {
            return PlayerPrefs.GetInt(CurrencyKeyPrefix + (int)type, 0);
        }

        public static void AddTotalCurrency(CurrencyType type, int amount)
        {
            int v = Mathf.Max(0, GetTotalCurrency(type) + amount);
            PlayerPrefs.SetInt(CurrencyKeyPrefix + (int)type, v);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] 중복 인스턴스 - 파괴", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (Player != null)
            {
                Player.Died -= OnPlayerDied;
            }
        }

        private void Start()
        {
            // 구독은 Start 에서 (모든 Awake 이후 = PlayerController.Instance 보장)
            if (Player != null)
            {
                Player.Died += OnPlayerDied;
            }
            EnterTitle();
        }

        // ------------------------------------------------------------------
        // 상태 전이 (UI 버튼에서 호출)
        // ------------------------------------------------------------------

        private void EnterTitle()
        {
            Current = State.Title;

            if (_titleScreen != null) _titleScreen.Show();
            if (_inventoryUI != null) _inventoryUI.HideInstant();
            if (_hud != null) _hud.SetActive(false);
            if (_resultScreen != null) _resultScreen.Hide();
            if (Player != null) Player.SetControlEnabled(false);
            if (_fader != null) _fader.FadeIn();
        }

        /// <summary>타이틀의 Play 버튼. 1번째 = 인벤토리 열기, 2번째 = 게임 시작.</summary>
        public void OnPlayPressed()
        {
            if (Current == State.Title)
            {
                OpenInventory();
            }
            else if (Current == State.Inventory)
            {
                StartDive();
            }
        }

        public void OpenInventory()
        {
            if (Current != State.Title)
            {
                return;
            }
            Current = State.Inventory;
            if (_inventoryUI != null) _inventoryUI.SlideIn();
            if (_titleScreen != null) _titleScreen.SetPlayMode();
        }

        /// <summary>인벤토리의 뒤로가기 버튼(선택).</summary>
        public void CloseInventory()
        {
            if (Current != State.Inventory)
            {
                return;
            }
            Current = State.Title;
            if (_inventoryUI != null) _inventoryUI.SlideOut();
            if (_titleScreen != null) _titleScreen.SetReadyMode();
        }

        /// <summary>인벤토리의 Dive 버튼. 실제 게임 시작.</summary>
        public void StartDive()
        {
            if (Current != State.Inventory && Current != State.Title)
            {
                return;
            }
            Current = State.Playing;

            if (_titleScreen != null) _titleScreen.Hide();
            if (_inventoryUI != null) _inventoryUI.SlideOut();
            if (_hud != null) _hud.SetActive(true);
            if (Player != null) Player.SetControlEnabled(true);
        }

        // ------------------------------------------------------------------
        // 사망 → 결과 → 리로드
        // ------------------------------------------------------------------

        private void OnPlayerDied()
        {
            if (Current == State.Playing)
            {
                StartCoroutine(EndRun());
            }
        }

        private IEnumerator EndRun()
        {
            Current = State.Ending;

            int score = Mathf.Max(0, Player != null ? Player.Depth : 0);
            int best = BestDepth;
            bool newBest = score > best;
            if (newBest)
            {
                best = score;
                BestDepth = best;
            }

            // 이번 런에서 모은 재화·장비를 영구 저장으로 적립
            RunWallet.Instance?.BankToTotal();
            RunInventory.Instance?.BankToOwned();

            yield return new WaitForSeconds(_deathPause);

            if (_map != null)
            {
                _map.SetStreamingEnabled(false);
            }

            // 사망 연출(스캐터 + 파티클)이 끝날 때까지 카메라 되감기를 미룬다. 안전상 최대 3초.
            PlayerAnimation deathAnim = Player != null ? Player.GetComponentInChildren<PlayerAnimation>() : null;
            float animGuard = 0f;
            while (deathAnim != null && deathAnim.DeathSequenceActive && animGuard < 3f)
            {
                animGuard += Time.deltaTime;
                yield return null;
            }

            if (_camera != null)
            {
                bool arrived = false;
                _camera.RewindToHome(_rewindSpeed, _rewindMinDuration, _rewindMaxDuration, () => arrived = true);
                while (!arrived)
                {
                    yield return null;
                }
            }

            if (_resultScreen != null)
            {
                _resultScreen.Show(score, best, newBest);
            }

            yield return new WaitForSeconds(_resultHold);

            if (_fader != null)
            {
                bool faded = false;
                _fader.FadeOut(() => faded = true);
                while (!faded)
                {
                    yield return null;
                }
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
