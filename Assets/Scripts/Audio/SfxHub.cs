using UnityEngine;
using Game.Map;
using Game.Player;
using Game.Economy;
using Game.Skills;

namespace Game.Audio
{
    /// <summary>
    /// 게임 이벤트 → 효과음 연결점. 씬 싱글톤들의 이벤트만 여기서 구독한다.
    /// (적/상자/보스는 각자 코드에서 <see cref="SfxPlayer"/> 를 직접 호출 — 풀링 대상이라 구독이 지저분)
    ///
    /// 배치: 항상 활성인 오브젝트에 하나 (SfxPlayer 옆).
    /// </summary>
    public class SfxHub : MonoBehaviour
    {
        private MapGenerator _map;
        private PlayerController _player;
        private RunWallet _wallet;
        private SkillSystem _skills;

        private void Start()
        {
            _map = MapGenerator.Instance;
            if (_map != null)
            {
                _map.DamageDealt += OnDamageDealt;
            }

            _player = PlayerController.Instance;
            if (_player != null)
            {
                _player.DamageTaken += OnPlayerDamaged;
                _player.Died += OnPlayerDied;
            }

            _wallet = RunWallet.Instance;
            if (_wallet != null)
            {
                _wallet.Changed += OnWalletChanged;
            }

            _skills = FindFirstObjectByType<SkillSystem>();
            if (_skills != null)
            {
                for (int i = 0; i < _skills.SlotCount; i++)
                {
                    Skill s = _skills.GetSlot(i);
                    if (s != null)
                    {
                        int slot = Mathf.Clamp(s.Slot, 0, 2);
                        s.Activated += () => SfxPlayer.Play(SfxId.SkillDash + slot);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_map != null) _map.DamageDealt -= OnDamageDealt;
            if (_player != null)
            {
                _player.DamageTaken -= OnPlayerDamaged;
                _player.Died -= OnPlayerDied;
            }
            if (_wallet != null) _wallet.Changed -= OnWalletChanged;
            // 스킬 Activated 는 익명 구독이라 씬 리로드 시 스킬과 함께 정리됨
        }

        private void OnDamageDealt(Vector3 pos, int amount, bool crit, bool isEntity)
        {
            SfxPlayer.PlayAt(crit ? SfxId.Crit : (isEntity ? SfxId.HitEnemy : SfxId.Dig), pos);
        }

        private void OnPlayerDamaged(int amount, Vector3 source) => SfxPlayer.Play(SfxId.PlayerHurt);

        private void OnPlayerDied() => SfxPlayer.Play(SfxId.PlayerDeath);

        private void OnWalletChanged() => SfxPlayer.Play(SfxId.CoinPickup);
    }
}
