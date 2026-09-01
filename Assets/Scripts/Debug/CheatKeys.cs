using UnityEngine;
using UnityEngine.InputSystem;
using Game.Player;
using Game.Equipment;

namespace Game.Debugging
{
    /// <summary>
    /// 인게임 치트키. <b>빌드에서도 동작</b> (G / M). 씬 배치 불필요 — 자동 생성.
    ///   G : 무적 토글
    ///   M : 모든 무기(검) 해금 토글
    ///   R : 모든 저장 데이터 리셋 (에디터 전용)
    /// 상태는 좌하단에 표시, 토글 시 잠깐 큰 메시지.
    /// </summary>
    public class CheatKeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[CheatKeys]");
            go.AddComponent<CheatKeys>();
            DontDestroyOnLoad(go);
        }

        private string _flash;
        private float _flashUntil;
        private GUIStyle _statusStyle;
        private GUIStyle _flashStyle;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.gKey.wasPressedThisFrame)
            {
                Show("GOD MODE: " + (PlayerStats.ToggleGodMode() ? "ON" : "OFF"));
            }
            if (kb.mKey.wasPressedThisFrame)
            {
                Show("ALL WEAPONS: " + (PlayerStats.ToggleAllSwordsUnlocked() ? "ON" : "OFF"));
            }
#if UNITY_EDITOR
            if (kb.rKey.wasPressedThisFrame)
            {
                PlayerStats.ResetAllProgress();
                Show("SAVE DATA RESET");
            }
#endif
        }

        private void Show(string msg)
        {
            _flash = msg;
            _flashUntil = Time.unscaledTime + 2f;
        }

        private void OnGUI()
        {
            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                _statusStyle.normal.textColor = Color.yellow;
                _flashStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                _flashStyle.normal.textColor = Color.yellow;
            }

            bool god = PlayerStats.GodMode;
            bool weapons = RunInventory.AllUnlocked;
            if (god || weapons)
            {
                string s = "[Cheats]" + (god ? "  GOD" : "") + (weapons ? "  ALL WEAPONS" : "");
                GUI.Label(new Rect(8f, Screen.height - 26f, 600f, 22f), s, _statusStyle);
            }

            if (Time.unscaledTime < _flashUntil && !string.IsNullOrEmpty(_flash))
            {
                GUI.Label(new Rect(8f, Screen.height - 64f, 600f, 34f), _flash, _flashStyle);
            }
        }
    }
}
