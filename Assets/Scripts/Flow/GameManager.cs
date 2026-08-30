using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Game.Player;
using Game.Map;
using Game.CameraRig;

namespace Game.Flow
{
    /// <summary>
    /// 게임 한 판의 전체 흐름을 관리하는 단일 상태 머신.
    ///
    ///   Title ──(아무 키/클릭)──▶ Playing ──(사망)──▶ Ending ──(씬 리로드)──▶ Title
    ///
    ///  - Title  : 시작 UI 표시, 플레이어 조작 정지, 페이드 인
    ///  - Playing: 시작 UI 숨김, HUD 표시, 조작 활성화
    ///  - Ending : 정지 → 카메라 되감기 → 이번/최고 심도 표시 → 페이드 아웃 → 같은 씬 리로드
    ///
    /// 항상 활성인 매니저 오브젝트에 붙일 것 (토글되는 UI 패널에 붙이지 말 것).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private CameraFollow _camera;
        [SerializeField] private MapGenerator _map;
        [SerializeField] private ScreenFader _fader;

        [Header("UI")]
        [Tooltip("시작 UI 패널. 기본 활성.")]
        [SerializeField] private GameObject _titleUI;
        [Tooltip("인게임 HUD. 기본 비활성.")]
        [SerializeField] private GameObject _hud;
        [SerializeField] private ResultScreen _resultScreen;

        [Header("사망 연출")]
        [Tooltip("사망 후 되감기 시작 전 정지 시간(초).")]
        [SerializeField] private float _deathPause = 0.4f;
        [Tooltip("카메라 되감기 속도(월드 단위/초).")]
        [SerializeField] private float _rewindSpeed = 16f;
        [Tooltip("되감기 최소/최대 시간(초).")]
        [SerializeField] private float _rewindMinDuration = 0.6f;
        [SerializeField] private float _rewindMaxDuration = 2.5f;
        [Tooltip("결과 표시를 유지하는 시간(초). 이후 페이드 아웃.")]
        [SerializeField] private float _resultHold = 2.5f;

        public enum State { Title, Playing, Ending }
        public State Current { get; private set; }

        private const string BestDepthKey = "best_depth";

        /// <summary>최고 심도(m). 씬/세션을 넘어 유지.</summary>
        public static int BestDepth
        {
            get => PlayerPrefs.GetInt(BestDepthKey, 0);
            private set { PlayerPrefs.SetInt(BestDepthKey, value); PlayerPrefs.Save(); }
        }

        // ------------------------------------------------------------------

        private void OnEnable()
        {
            if (_player != null)
            {
                _player.Died += OnPlayerDied;
            }
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.Died -= OnPlayerDied;
            }
        }

        private void Start()
        {
            EnterTitle();
        }

        private void Update()
        {
            if (Current == State.Title && StartPressed())
            {
                BeginRun();
            }
        }

        // ------------------------------------------------------------------
        // 상태 전이
        // ------------------------------------------------------------------

        private void EnterTitle()
        {
            Current = State.Title;

            if (_titleUI != null) _titleUI.SetActive(true);
            if (_hud != null) _hud.SetActive(false);
            if (_player != null) _player.SetControlEnabled(false);
            if (_fader != null) _fader.FadeIn();
        }

        private void BeginRun()
        {
            Current = State.Playing;

            if (_titleUI != null) _titleUI.SetActive(false);
            if (_hud != null) _hud.SetActive(true);
            if (_player != null) _player.SetControlEnabled(true);
        }

        private void OnPlayerDied()
        {
            Debug.Log($"[GameManager] OnPlayerDied  state={Current}  resultScreen={(_resultScreen != null)}", this);
            if (Current == State.Playing)
            {
                StartCoroutine(EndRun());
            }
        }

        private IEnumerator EndRun()
        {
            Current = State.Ending;

            int score = Mathf.Max(0, _player != null ? _player.Depth : 0);
            int best = BestDepth;
            bool newBest = score > best;
            if (newBest)
            {
                best = score;
                BestDepth = best;
            }

            yield return new WaitForSeconds(_deathPause);

            if (_map != null)
            {
                _map.SetStreamingEnabled(false);
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

        // ------------------------------------------------------------------

        private static bool StartPressed()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame)
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }
    }
}
