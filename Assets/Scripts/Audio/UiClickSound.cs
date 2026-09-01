using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Skills;

namespace Game.Audio
{
    /// <summary>
    /// 씬의 모든 UGUI <see cref="Button"/> 의 onClick 에 공용 클릭 효과음을 자동으로 붙인다.
    /// 버튼 프리팹을 하나하나 건드리지 않기 위한 임시/전역 방식.
    ///
    /// - 활성화 시 1회 스캔 + <see cref="_rescanInterval"/> 마다 재스캔
    ///   (검 슬롯처럼 런타임에 Instantiate 되는 버튼 대응).
    /// - 인게임 스킬 버튼(<see cref="SkillButtonUI"/>)은 자체 스킬 효과음이 있어 제외.
    ///
    /// 배치: SfxPlayer 옆(항상 활성인 오브젝트)에 하나.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UiClickSound : MonoBehaviour
    {
        [SerializeField] private SfxId _sfx = SfxId.UiClick;
        [Tooltip("새 버튼을 찾기 위해 다시 훑는 주기(초). 0 이면 활성화 시 1회만.")]
        [SerializeField] private float _rescanInterval = 1f;

        private readonly HashSet<Button> _hooked = new HashSet<Button>();
        private float _timer;

        private void OnEnable()
        {
            _timer = 0f;
            Scan();
        }

        private void Update()
        {
            if (_rescanInterval <= 0f)
            {
                return;
            }
            _timer += Time.unscaledDeltaTime;
            if (_timer >= _rescanInterval)
            {
                _timer = 0f;
                Scan();
            }
        }

        private void Scan()
        {
            Button[] all = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            SfxId id = _sfx;
            foreach (Button b in all)
            {
                if (b == null || !_hooked.Add(b))
                {
                    continue; // 이미 처리했거나 파괴됨
                }
                if (b.GetComponentInParent<SkillButtonUI>() != null)
                {
                    continue; // 스킬 버튼은 스킬 발동 효과음으로 대체
                }
                b.onClick.AddListener(() => SfxPlayer.Play(id));
            }
        }
    }
}
