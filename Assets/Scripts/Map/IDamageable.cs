namespace Game.Map
{
    /// <summary>피해를 받을 수 있는 대상 (지층 엔티티, 나중에 보스 등).</summary>
    public interface IDamageable
    {
        /// <summary>피해를 적용한다. 이 피해로 파괴/사망했으면 true.</summary>
        bool TakeDamage(int amount);
    }
}
