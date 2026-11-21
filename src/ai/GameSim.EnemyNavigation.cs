public partial class GameSim
{
    const int ENEMY_NAVIGATION_LIMIT = 2;
    bool LimitEnemyNavigation;
    int EnemyThinkCursor;
    public int EnemyNavigationUpdatesThisTick { get; private set; }
}
