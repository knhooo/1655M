namespace Game.Map
{
    /// <summary>
    /// 지층 종류. 종류마다 파괴 난이도(HP)와 연출/드롭이 달라진다.
    /// </summary>
    public enum StrataType
    {
        Empty = 0, // 빈 칸 (파낸 공간 / 동굴)
        Soil,      // 흙   - 약함
        Rock,      // 암반 - 보통
        Ore,       // 광맥 - 단단함, 골드 드롭
        // TODO: Lava(피해), Unstable(붕괴) 등 추가
    }

    /// <summary>
    /// 그리드 한 칸(셀)의 런타임 데이터.
    /// HP 등 상태는 "데이터"인 이 구조체가 들고 있고, <see cref="BlockView"/>는
    /// 풀링으로 자주 붙었다 떨어지므로 상태를 갖지 않는다.
    /// </summary>
    [System.Serializable]
    public struct BlockData
    {
        public StrataType Type;
        public int Hp;
        public int MaxHp;

        public BlockData(StrataType type, int hp)
        {
            Type = type;
            Hp = hp;
            MaxHp = hp;
        }

        public readonly bool IsSolid => Type != StrataType.Empty && Hp > 0;

        public readonly float HpNormalized => MaxHp > 0 ? (float)Hp / MaxHp : 0f;

        public static BlockData Empty => new BlockData(StrataType.Empty, 0);
    }
}
