# Design

CalradiaStrategicMind is intended to improve strategic AI behavior on the Bannerlord campaign map. The first project version is only a safe loading skeleton; the systems below are future goals.

## Future Goals

- Strategic evaluation of faction strength, clan strength, armies, parties, and settlements.
- Better target selection for lords and armies.
- Army logic that considers distance, enemy strength, cohesion, food, recent losses, and kingdom priorities.
- Settlement defense logic that identifies threatened towns, castles, villages, and nearby armies.
- More useful garrison decisions based on settlement value, risk, and available troops.
- Siege decisions that account for defenders, relief armies, distance, food, and expected reinforcement.
- Diplomacy support for war pressure, exhaustion, border threats, and faction priorities.
- MCM settings in a later stage for tuning behavior without recompiling.
- Russian localization after core settings and player-facing text exist.

## Safety Principles

- Add behavior in small increments.
- Prefer observation and logging before changing AI decisions.
- Keep risky logic behind `SafeExecutor`.
- Use null checks around campaign objects.
- Do not add dependencies such as Harmony or MCM until a specific task needs them.
