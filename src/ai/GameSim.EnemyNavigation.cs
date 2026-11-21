public partial class GameSim
{
    const int ENEMY_NAVIGATION_LIMIT = 2;
    bool LimitEnemyNavigation;
    int EnemyThinkCursor;
    float EnemyStepAccumulator;
    public float EnemyBodyInterval = .05f;
    public int EnemyBodySteps { get; private set; }
    public int EnemyNavigationUpdatesThisTick { get; private set; }
}
