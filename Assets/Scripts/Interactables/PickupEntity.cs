using System;
using UnityEngine;
using Game.Player;
using Game.Audio;

namespace Game.Interactables
{
    /// <summary>
    /// 재화 광맥처럼 격자에 박히는 <b>획득형 아이템</b>의 베이스.
    /// 플레이어가 인접하면(또는 때리면) 자동 획득 → <see cref="OnCollected"/> 효과 실행 후 사라진다.
    /// 프리팹을 만들어 <c>MapGenerator._entityTable</c> 에 넣는다 (재화·적·상자와 동일 경로).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class PickupEntity : Game.Map.GridEntity
    {
        [Header("Pickup")]
        [SerializeField] private SfxId _pickupSfx = SfxId.CoinPickup;
        [Tooltip("획득 위치에 잠깐 띄울 이펙트 (선택).")]
        [SerializeField] private GameObject _pickupVfx;
        [SerializeField] private float _pickupVfxLifetime = 1.5f;

        /// <summary>획득됨: 표시용 라벨(예 "HP +200").</summary>
        public event Action<string> Collected;

        private bool _collected;

        protected override void OnPlaced() => _collected = false;

        private void Update()
        {
            if (_collected || Map == null)
            {
                return;
            }
            PlayerController pc = Map.Player;
            if (pc != null && pc.IsAlive && IsPlayerAdjacent(pc.Column, pc.Row))
            {
                Collect(pc);
            }
        }

        /// <summary>닿기 전에 때렸을 때도 획득.</summary>
        public override bool ApplyDamage(int amount)
        {
            if (amount <= 0 || _collected)
            {
                return false;
            }
            PlayerController pc = Map != null ? Map.Player : null;
            if (pc != null)
            {
                Collect(pc);
            }
            return true;
        }

        private void Collect(PlayerController pc)
        {
            if (_collected)
            {
                return;
            }
            _collected = true;

            string label = OnCollected(pc);

            if (_pickupSfx != SfxId.None)
            {
                SfxPlayer.PlayAt(_pickupSfx, transform.position);
            }
            if (_pickupVfx != null)
            {
                GameObject g = Instantiate(_pickupVfx, transform.position, Quaternion.identity);
                Destroy(g, Mathf.Max(0.1f, _pickupVfxLifetime));
            }
            Collected?.Invoke(label);
            Map.ClearEntity(this);
        }

        /// <summary>획득 효과 적용. 반환값은 표시용 라벨.</summary>
        protected abstract string OnCollected(PlayerController pc);
    }
}
