# API Audit

## GameSystem public members

### `Assets/Landsong/Scripts/GameSystem/GameSystem.cs`

```text
GameOverReason                             {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:20
Inventory                                  {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:86
Turn                                       {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:89
Dynasty                                    {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:92
Buildings                                  {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:95
Events                                     {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:98
Technology                                 {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:101
Expeditions                                {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:104
Talents                                    {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:107
Inheritance                                {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:110
BuildingSelection                          => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:113
BuildingCatalog                            => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:116
TechnologyCatalog                          => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:119
Population                                 => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:122
DynastyName                                => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:125
HasPalace                                  => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:128
IsGameOver                                 {  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:131
CurrentResearchTechnologyId                => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:138
CurrentResearchProgress                    => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:143
CurrentResearchRequiredPoints              => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:146
UnlockedTechnologies                       => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:149
ExpeditionStates                           => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:152
ExpeditionDestinationCatalog               => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:155
TalentPool                                 => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:156
TalentOffers                               => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:158
TalentSlots                                => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:160
TalentCatalog                              => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:162
RoyalTraitCatalog                          => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:163
CurrentKing                                => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:164
CurrentQueen                               => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:165
RoyalCharacters                            => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:166
UnlockedBuildingBlueprintIds               => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:168
HasActiveExpeditionSubsidyPenalty          => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:169
ExpeditionSubsidyPenaltyStacks             => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:171
ExpeditionSubsidyPenaltyActiveUntilTurn    => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:173
ActiveExpeditionTeamCount                  => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:175
ExpeditionTeamCapacity                     => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:177
ExpeditionRewardYieldBonus                 => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:179
ExpeditionRewardYieldMultiplier            => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:180
GetJobAttractionModifiers                  (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:181
ReinitializeInventory                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:341
SetBuildingCatalog                         (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:345
SetTechnologyCatalog                       (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:350
IsTechnologyUnlocked                       (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:356
UnlockTechnology                           (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:367
TryStartTechnologyResearch                 (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:385
TryStartTechnologyResearch                 (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:403
CaptureUnlockedTechnologies                (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:421
CaptureTechnologyData                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:429
RestoreUnlockedTechnologies                (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:446
RestoreTechnologyData                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:478
CaptureUnlockedBuildingBlueprints          (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:504
RestoreBuildingBlueprintData               (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:511
IsBuildingBlueprintUnlocked                (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:533
UnlockBuildingBlueprint                    (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:540
SetExpeditionDestinationCatalog            (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:552
CaptureExpeditionData                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:558
RestoreExpeditionData                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:563
GetExpeditionDestinations                  (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:576
TryStartExpedition                         (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:588
TryClaimExpeditionRewards                  (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:623
SetTalentCatalog                           (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:652
CaptureTalentData                          (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:658
RestoreTalentData                          (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:663
TryRefreshTalents                          (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:675
TryRecruitTalentOffer                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:700
TryAssignTalent                            (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:725
TryUnassignTalentSlot                      (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:750
TryUnassignTalent                          (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:775
TryUpgradeTalent                           (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:800
AddTalentExperience                        (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:826
SetRoyalTraitCatalog                       (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:836
CaptureInheritanceData                     (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:842
RestoreInheritanceData                     (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:847
TryBirthPrince                             (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:859
TryAbdicateCurrentKing                     (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:895
TryAddRoyalAcquiredTrait                   (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:918
RegisterBuilding                           (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1553
UnregisterBuilding                         (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1598
CurrentTurn                                => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1713
IsAdvancingTurn                            => Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1717
NextTurn                                   (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1722
ApplyTalentResearchPoints                  (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1916
ApplyRoyalResearchPoints                   (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:1922
EndGame                                    (  Assets/Landsong/Scripts/GameSystem/GameSystem.cs:2052
```

### `Assets/Landsong/Scripts/GameSystem/GameSystem.TurnState.cs`

```text
GameSystem                                 {  Assets/Landsong/Scripts/GameSystem/GameSystem.TurnState.cs:6
```

### `Assets/Landsong/Scripts/GameSystem/GameServices.cs`

```text
GameServices                               {  Assets/Landsong/Scripts/GameSystem/GameServices.cs:17
Inventory                                  => Assets/Landsong/Scripts/GameSystem/GameServices.cs:25
Turn                                       => Assets/Landsong/Scripts/GameSystem/GameServices.cs:27
Dynasty                                    => Assets/Landsong/Scripts/GameSystem/GameServices.cs:28
Buildings                                  => Assets/Landsong/Scripts/GameSystem/GameServices.cs:29
Events                                     => Assets/Landsong/Scripts/GameSystem/GameServices.cs:30
Technology                                 => Assets/Landsong/Scripts/GameSystem/GameServices.cs:31
Quest                                      => Assets/Landsong/Scripts/GameSystem/GameServices.cs:32
Expeditions                                => Assets/Landsong/Scripts/GameSystem/GameServices.cs:33
Talents                                    => Assets/Landsong/Scripts/GameSystem/GameServices.cs:34
Inheritance                                => Assets/Landsong/Scripts/GameSystem/GameServices.cs:35
GameSystem                                 {  Assets/Landsong/Scripts/GameSystem/GameServices.cs:37
Services                                   => Assets/Landsong/Scripts/GameSystem/GameServices.cs:41
Quest                                      {  Assets/Landsong/Scripts/GameSystem/GameServices.cs:43
```

### `Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs`

```text
GameSystem                                 {  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:13
Quests                                     => Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:37
ReinitializeQuests                         (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:42
AddGameplayDebugItem                       (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:50
AddGameplayDebugGold                       (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:62
TryAddGameplayDebugRandomQuest             (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:67
CaptureQuestData                           (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:72
RestoreQuestData                           (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:77
TrySubmitQuestResources                    (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:93
TrySubmitQuestResources                    (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:98
TryAbandonQuest                            (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:103
TryAbandonQuest                            (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:108
TryClaimQuestRewards                       (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:113
TryClaimQuestRewards                       (  Assets/Landsong/Scripts/QuestSystem/GameSystem.Quest.cs:118
```

## DataManager public members

```text
GameDataSaveMode                           {  Assets/Landsong/Scripts/AppSystem/DataManager.cs:10
AppData                                    => Assets/Landsong/Scripts/AppSystem/DataManager.cs:28
HasLoadedAppData                           {  Assets/Landsong/Scripts/AppSystem/DataManager.cs:30
HasLoadedGameDataIndex                     {  Assets/Landsong/Scripts/AppSystem/DataManager.cs:31
SaveRootPath                               {  Assets/Landsong/Scripts/AppSystem/DataManager.cs:32
GameDataMetaList                           => Assets/Landsong/Scripts/AppSystem/DataManager.cs:40
CurrentGameData                            {  Assets/Landsong/Scripts/AppSystem/DataManager.cs:41
Initialize                                 (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:53
LoadAppData                                (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:61
SaveAppData                                (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:87
EnsureAppDataLoaded                        (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:95
MarkFirstLaunchFinished                    (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:103
SetBgmVolume                               (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:110
SetSfxVolume                               (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:115
SetAmbienceVolume                          (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:120
SetAudioMasterVolume                       (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:125
SetAudioVolumeGroup                        (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:130
SetAudioChannelVolume                      (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:135
SetMuted                                   (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:140
LoadGameDataIndex                          (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:145
SaveGameDataIndex                          (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:166
CreateNewGame                              (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:176
SaveCurrentGame                            (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:199
SaveGameData                               (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:203
OverwriteSaveGameData                      (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:208
SaveNewGameData                            (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:213
QuickSaveGameData                          (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:218
AutoSaveGameData                           (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:223
SaveGameData                               (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:228
SetCurrentGameSaveName                     (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:233
GetDefaultCurrentGameSaveName              (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:252
SetLastSelectedBuilding                    (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:272
LoadGameData                               (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:278
LoadLastGameData                           (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:318
DeleteGameData                             (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:324
CreateBackup                               (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:365
GetLastGameDataMeta                        (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:384
GetAllGameDataMeta                         (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:391
RestoreCurrentGameDataToRuntime            (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:397
RestoreCurrentGameDataToRuntimeRoutine     (  Assets/Landsong/Scripts/AppSystem/DataManager.cs:415
```

## External usages of GameSystem public members

### `ActiveExpeditionTeamCount` (2)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:328: SetText(expeditionTeamCountLabel, $"{gameSystem.ActiveExpeditionTeamCount}/{gameSystem.ExpeditionTeamCapacity}");
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:412: && gameSystem.ActiveExpeditionTeamCount < gameSystem.ExpeditionTeamCapacity;
```

### `AddGameplayDebugGold` (1)

```text
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:258: var added = gameSystem.AddGameplayDebugGold();
```

### `AddGameplayDebugItem` (1)

```text
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:361: var added = gameSystem.AddGameplayDebugItem(definition.ItemId, amount);
```

### `ApplyRoyalResearchPoints` (1)

```text
Assets/Landsong/Scripts/InheritanceSystem/RoyalInheritanceService.cs:1331: context.ApplyRoyalResearchPoints(amount, turnNumber, character.DisplayName);
```

### `ApplyTalentResearchPoints` (1)

```text
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1291: context.ApplyTalentResearchPoints(amount, turnNumber, talent.DisplayName);
```

### `BuildingCatalog` (2)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:323: var catalog = gameSystem.BuildingCatalog == null ? BuildingCatalog.Instance : gameSystem.BuildingCatalog;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Building.cs:288: buildingCatalog = gameSystem == null ? null : gameSystem.BuildingCatalog;
```

### `BuildingSelection` (4)

```text
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:677: BuildingSelectionController selectionController = gameSystem == null ? null : gameSystem.BuildingSelection;
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:223: var selectedBuilding = gameSystem == null || gameSystem.BuildingSelection == null
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:225: : gameSystem.BuildingSelection.SelectedBuilding;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_BuildingSelectionView.cs:82: selectionController = gameSystem == null ? null : gameSystem.BuildingSelection;
```

### `Buildings` (15)

```text
Assets/Landsong/Scripts/QuestSystem/QuestService.cs:48: private BuildingService Buildings => context.Buildings;
Assets/Landsong/Scripts/BuildingSystem/BuildingSelectionController.cs:195: buildings = gameSystem == null ? null : gameSystem.Buildings;
Assets/Landsong/Scripts/BuildingSystem/BuildingPlacementController.cs:1278: if (gameSystem != null && gameSystem.Buildings != null)
Assets/Landsong/Scripts/BuildingSystem/BuildingPlacementController.cs:1280: return gameSystem.Buildings;
Assets/Landsong/Scripts/BuildingSystem/BuildingPlacementController.cs:1284: return gameSystem == null ? null : gameSystem.Buildings;
Assets/Landsong/Scripts/BuildingSystem/BuildingPlacementController.cs:1290: return gameSystem == null ? null : gameSystem.Buildings;
Assets/Landsong/Scripts/AppSystem/LSScenes.cs:261: if (Landsong.GameSystem.TryGetInstance(out var gameSystem) && gameSystem.Buildings != null)
Assets/Landsong/Scripts/AppSystem/LSScenes.cs:263: var buildings = gameSystem.Buildings.Buildings;
Assets/Landsong/Scripts/TalentSystem/TalentDefinitions.cs:225: return context == null || context.Buildings == null || context.Buildings.Buildings == null
Assets/Landsong/Scripts/TalentSystem/TalentDefinitions.cs:227: : context.Buildings.Buildings.Count;
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:678: IReadOnlyList<BuildingBase> runtimeBuildings = gameSystem == null || gameSystem.Buildings == null
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:680: : gameSystem.Buildings.Buildings;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Building.cs:290: buildings = gameSystem == null ? null : gameSystem.Buildings;
Assets/Landsong/Scripts/UI/UIPanel_Game/BuildingStatusMarkerManager.cs:109: SetBuildingService(gameSystem == null ? null : gameSystem.Buildings);
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_BuildingStatusOverview.cs:81: buildings = gameSystem == null ? null : gameSystem.Buildings;
```

### `CaptureExpeditionData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:39: ? gameSystem.CaptureExpeditionData()
```

### `CaptureInheritanceData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:45: ? gameSystem.CaptureInheritanceData()
```

### `CaptureQuestData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:36: ? gameSystem.CaptureQuestData()
```

### `CaptureTalentData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:42: ? gameSystem.CaptureTalentData()
```

### `CaptureTechnologyData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:30: ? gameSystem.CaptureTechnologyData()
```

### `CaptureUnlockedBuildingBlueprints` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:47: gameData.UnlockedBuildingBlueprintIds = gameSystem.CaptureUnlockedBuildingBlueprints();
```

### `CaptureUnlockedTechnologies` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:33: ? gameSystem.CaptureUnlockedTechnologies()
```

### `CurrentTurn` (17)

```text
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:475: public bool IsSubsidyPenaltyActive => IsSubsidyPenaltyActiveAt(context == null ? 1 : context.CurrentTurn);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:504: var currentTurn = context == null ? 1 : context.CurrentTurn;
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:569: var availability = EvaluateDestination(destination, context == null ? 1 : context.CurrentTurn);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:629: var currentTurn = context.CurrentTurn;
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:772: ClearExpiredPenalty(context == null ? 1 : context.CurrentTurn);
Assets/Landsong/Scripts/QuestSystem/QuestService.cs:52: private int CurrentTurn => context.CurrentTurn;
Assets/Landsong/Scripts/AppSystem/DataManager.cs:261: return GameData.FormatDefaultSaveName(runtimeDynastyName, gameSystem.CurrentTurn);
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:692: lastRefreshTurn = context == null ? 0 : context.CurrentTurn;
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:26: gameData.CurrentTurn = services.Turn == null ? gameSystem.CurrentTurn : services.Turn.CurrentTurn;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_QuestItem.cs:277: var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.CurrentTurn;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:264: gameSystem.CurrentTurn,
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:279: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn);
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:492: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:535: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:545: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:496: txt_TurnCount.text = gameSystem == null ? string.Empty : gameSystem.CurrentTurn.ToString();
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanelHUD_Quest.cs:509: var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.CurrentTurn;
```

### `Dynasty` (8)

```text
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:594: if (context == null || context.Dynasty == null || !context.Dynasty.TryConsumePopulation(population))
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:603: context.Dynasty.AddPopulation(population);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:610: context.Dynasty.AddPopulation(population);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:617: context.Dynasty.AddPopulation(population);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:624: context.Dynasty.AddPopulation(population);
Assets/Landsong/Scripts/BuildingSystem/BuildingJobSystem.cs:433: var population = gameSystem == null || gameSystem.Dynasty == null ? 0 : gameSystem.Dynasty.Population;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:131: dynasty = gameSystem == null ? null : gameSystem.Dynasty;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:270: dynasty = gameSystem == null ? null : gameSystem.Dynasty;
```

### `Events` (3)

```text
Assets/Landsong/Scripts/QuestSystem/QuestService.cs:50: private GameEventService Events => context.Events;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_BuildingEventMessageList.cs:130: var resolvedEvents = gameSystem == null ? null : gameSystem.Events;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_BuildingEventMessageList.cs:279: gameEvents = gameSystem == null ? null : gameSystem.Events;
```

### `ExpeditionDestinationCatalog` (2)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:624: if (gameSystem == null || gameSystem.ExpeditionDestinationCatalog == null || string.IsNullOrWhiteSpace(selectedDestinationId))
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:629: return gameSystem.ExpeditionDestinationCatalog.TryGetDestination(selectedDestinationId, out var destination)
```

### `ExpeditionRewardYieldMultiplier` (2)

```text
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:897: var baseMultiplier = context == null ? 1f : context.ExpeditionRewardYieldMultiplier;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:863: var rewardYieldMultiplier = gameSystem == null ? 1f : gameSystem.ExpeditionRewardYieldMultiplier;
```

### `ExpeditionStates` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:649: var expeditions = gameSystem.ExpeditionStates;
```

### `ExpeditionSubsidyPenaltyActiveUntilTurn` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:317: $"补贴不足惩罚 {gameSystem.ExpeditionSubsidyPenaltyStacks} 层，持续至第 {gameSystem.ExpeditionSubsidyPenaltyActiveUntilTurn} 回合");
```

### `ExpeditionSubsidyPenaltyStacks` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:317: $"补贴不足惩罚 {gameSystem.ExpeditionSubsidyPenaltyStacks} 层，持续至第 {gameSystem.ExpeditionSubsidyPenaltyActiveUntilTurn} 回合");
```

### `ExpeditionTeamCapacity` (2)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:328: SetText(expeditionTeamCountLabel, $"{gameSystem.ActiveExpeditionTeamCount}/{gameSystem.ExpeditionTeamCapacity}");
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:412: && gameSystem.ActiveExpeditionTeamCount < gameSystem.ExpeditionTeamCapacity;
```

### `Expeditions` (8)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:277: var availability = destination == null || gameSystem == null || gameSystem.Expeditions == null
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:279: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn);
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:490: gameSystem.Expeditions == null
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:492: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:533: gameSystem == null || gameSystem.Expeditions == null || destination == null
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:535: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:543: gameSystem == null || gameSystem.Expeditions == null || destination == null
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:545: : gameSystem.Expeditions.EvaluateDestination(destination, gameSystem.CurrentTurn));
```

### `GetExpeditionDestinations` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:247: var destinations = gameSystem.GetExpeditionDestinations(includeUnavailableDestinations);
```

### `HasActiveExpeditionSubsidyPenalty` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:309: if (gameSystem == null || !gameSystem.HasActiveExpeditionSubsidyPenalty)
```

### `Inheritance` (3)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inheritance.cs:176: var service = gameSystem == null ? null : gameSystem.Inheritance;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inheritance.cs:201: var service = gameSystem == null ? null : gameSystem.Inheritance;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inheritance.cs:223: var service = gameSystem == null ? null : gameSystem.Inheritance;
```

### `Inventory` (48)

```text
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:608: if (context.Inventory == null)
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:615: if (!HasSuppliesInInventory(context.Inventory, normalizedSupplies))
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:622: if (!RemoveSuppliesFromInventory(context.Inventory, normalizedSupplies))
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:702: if (context == null || context.Inventory == null)
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:794: && context.Inventory != null
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:833: if (context == null || context.Inventory == null || string.IsNullOrWhiteSpace(goldItemId))
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:839: var available = context.Inventory.GetQuantity(goldItemId);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:843: context.Inventory.RemoveItem(goldItemId, paid);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:970: if (context == null || context.Inventory == null)
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:983: return context.Inventory.CanAddItems(itemAmounts);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:989: if (reward != null && reward.IsValid && !context.Inventory.CanAddItem(reward.ItemId, reward.Amount))
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:1000: if (context == null || context.Inventory == null)
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:1013: context.Inventory.TryAddItems(itemAmounts);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:1022: context.Inventory.TryAddItem(reward.ItemId, reward.Amount);
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:1100: var catalog = context == null || context.Inventory == null
Assets/Landsong/Scripts/ExpeditionSystem/ExpeditionService.cs:1102: : context.Inventory.ItemCatalog;
Assets/Landsong/Scripts/InheritanceSystem/RoyalInheritanceService.cs:1308: if (context.Inventory == null || effect.ItemDefinition == null || amount <= 0)
Assets/Landsong/Scripts/InheritanceSystem/RoyalInheritanceService.cs:1313: var added = context.Inventory.AddItem(effect.ItemDefinition, amount);
Assets/Landsong/Scripts/QuestSystem/QuestService.cs:47: private InventoryService Inventory => context.Inventory;
Assets/Landsong/Scripts/BuildingSystem/BuildingService.cs:100: var inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/BuildingSystem/BuildingService.cs:117: var inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/BuildingSystem/BuildingAvailabilityEvaluator.cs:34: var inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/TechnologySystem/TechnologyEffects.cs:51: if (context == null || context.Inventory == null || itemDefinition == null || Amount <= 0)
Assets/Landsong/Scripts/TechnologySystem/TechnologyEffects.cs:56: var added = context.Inventory.AddItem(itemDefinition, Amount);
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:657: && context.Inventory != null
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:659: && context.Inventory.HasItem(goldItemId, refreshGoldCost);
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1157: if (context == null || context.Inventory == null)
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1169: if (!context.Inventory.HasItem(goldItemId, refreshGoldCost))
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1175: if (!context.Inventory.TryRemoveItem(goldItemId, refreshGoldCost))
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1196: if (context == null || context.Inventory == null || string.IsNullOrWhiteSpace(goldItemId))
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1202: var available = context.Inventory.GetQuantity(goldItemId);
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1206: context.Inventory.RemoveItem(goldItemId, salaryPaid);
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1268: if (context.Inventory == null || effect.ItemDefinition == null || amount <= 0)
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1273: var added = context.Inventory.AddItem(effect.ItemDefinition, amount);
Assets/Landsong/Scripts/TalentSystem/TalentDefinitions.cs:157: && context.Inventory != null
Assets/Landsong/Scripts/TalentSystem/TalentDefinitions.cs:161: var currentAmount = context.Inventory.GetQuantity(itemDefinition.ItemId);
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:302: var catalog = gameSystem.Inventory == null ? null : gameSystem.Inventory.ItemCatalog;
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:354: var catalog = gameSystem.Inventory == null ? null : gameSystem.Inventory.ItemCatalog;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:183: var inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:234: var available = service.SalaryGoldItemDefinition != null && gameSystem != null && gameSystem.Inventory != null
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:235: ? gameSystem.Inventory.GetQuantity(service.SalaryGoldItemDefinition.ItemId)
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Building.cs:289: inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inventory.cs:180: if (gameSystem.Inventory == null)
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inventory.cs:185: inventory = gameSystem.Inventory;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:130: inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:271: inventory = gameSystem == null ? null : gameSystem.Inventory;
Assets/Landsong/Scripts/UI/UIPanel_Game/BuildingResourceFloatTextManager.cs:357: var catalog = gameSystem == null || gameSystem.Inventory == null
Assets/Landsong/Scripts/UI/UIPanel_Game/BuildingResourceFloatTextManager.cs:359: : gameSystem.Inventory.ItemCatalog;
```

### `IsAdvancingTurn` (5)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:42: if (gameSystem != null && gameSystem.IsAdvancingTurn)
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:502: var isProcessing = gameSystem != null && gameSystem.IsAdvancingTurn;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:510: if (gameSystem == null || gameSystem.IsGameOver || gameSystem.IsAdvancingTurn)
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:552: while (gameSystem != null && gameSystem.IsAdvancingTurn)
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:577: SetNextTurnButtonVisible(gameSystem == null || !gameSystem.IsAdvancingTurn);
```

### `IsBuildingBlueprintUnlocked` (2)

```text
Assets/Landsong/Scripts/BuildingSystem/BuildingAvailabilityEvaluator.cs:56: return gameSystem != null && gameSystem.IsBuildingBlueprintUnlocked(definition.BuildingId);
Assets/Landsong/Scripts/Condition/GameCondition.cs:67: && context.IsBuildingBlueprintUnlocked(BuildingPrefab.Definition.BuildingId);
```

### `IsGameOver` (3)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:510: if (gameSystem == null || gameSystem.IsGameOver || gameSystem.IsAdvancingTurn)
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:564: SetNextTurnButtonVisible(gameSystem == null || !gameSystem.IsGameOver);
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:588: btn_下回合.interactable = visible && gameSystem != null && !gameSystem.IsGameOver;
```

### `IsTechnologyUnlocked` (1)

```text
Assets/Landsong/Scripts/Condition/GameCondition.cs:53: && context.IsTechnologyUnlocked(TechnologyDefinition.TechnologyId);
```

### `NextTurn` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:516: gameSystem.NextTurn();
```

### `Population` (1)

```text
Assets/Landsong/Scripts/TalentSystem/TalentDefinitions.cs:145: TalentEffectValueType.BasedOnPopulation => scaledValue * Mathf.Max(0, context == null ? 0 : context.Population),
```

### `Quests` (2)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Quest.cs:97: var quests = gameSystem.Quests;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanelHUD_Quest.cs:201: var quests = gameSystem == null ? null : gameSystem.Quests;
```

### `RegisterBuilding` (2)

```text
Assets/Landsong/Scripts/BuildingSystem/BuildingBase.cs:210: Landsong.GameSystem.Instance.RegisterBuilding(this);
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:349: gameSystem.RegisterBuilding(building);
```

### `ReinitializeInventory` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inventory.cs:182: gameSystem.ReinitializeInventory();
```

### `RestoreBuildingBlueprintData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:149: gameSystem.RestoreBuildingBlueprintData(gameData.UnlockedBuildingBlueprintIds);
```

### `RestoreExpeditionData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:157: gameSystem.RestoreExpeditionData(gameData.ExpeditionData);
```

### `RestoreInheritanceData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:159: gameSystem.RestoreInheritanceData(gameData.RoyalInheritanceData);
```

### `RestoreQuestData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:164: gameSystem.RestoreQuestData(gameData.QuestData);
```

### `RestoreTalentData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:158: gameSystem.RestoreTalentData(gameData.TalentData);
```

### `RestoreTechnologyData` (1)

```text
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:148: gameSystem.RestoreTechnologyData(gameData.TechnologyData, gameData.UnlockedTechnologies);
```

### `Services` (8)

```text
Assets/Landsong/Scripts/AppSystem/DataManager.cs:258: var runtimeDynastyName = gameSystem.Services.Dynasty == null
Assets/Landsong/Scripts/AppSystem/DataManager.cs:260: : gameSystem.Services.Dynasty.DynastyName;
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:25: var services = gameSystem.Services;
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:86: gameSystem.Services.Quest?.EndRuntimeRestore();
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:110: gameSystem.Services.Quest?.EndRuntimeRestore();
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:151: var services = gameSystem.Services;
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:305: if (saveData == null || gameSystem == null || gameSystem.Services.Buildings == null)
Assets/Landsong/Scripts/Persistence/GameRuntimeSnapshotService.cs:331: if (!gameSystem.Services.Buildings.TryPlace(
```

### `TalentOffers` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:250: var offers = gameSystem == null ? null : gameSystem.TalentOffers;
```

### `TalentPool` (2)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:276: var talents = gameSystem == null ? null : gameSystem.TalentPool;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:343: var talents = gameSystem == null ? null : gameSystem.TalentPool;
```

### `TalentSlots` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:311: var slots = gameSystem == null ? null : gameSystem.TalentSlots;
```

### `Talents` (4)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:208: var service = gameSystem == null ? null : gameSystem.Talents;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:277: var service = gameSystem == null ? null : gameSystem.Talents;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:354: return gameSystem == null || gameSystem.Talents == null
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:356: : gameSystem.Talents.FindOwnedTalent(selectedTalentInstanceId);
```

### `Technology` (3)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Technology.cs:108: technology = gameSystem == null ? null : gameSystem.Technology;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Building.cs:291: technology = gameSystem == null ? null : gameSystem.Technology;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:272: technology = gameSystem == null ? null : gameSystem.Technology;
```

### `TryAbandonQuest` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Quest.cs:332: gameSystem.TryAbandonQuest(quest);
```

### `TryAbdicateCurrentKing` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inheritance.cs:350: var abdicated = gameSystem.TryAbdicateCurrentKing(out var succession);
```

### `TryAddGameplayDebugRandomQuest` (1)

```text
Assets/Landsong/Scripts/Debug/LSDebugManager.cs:280: if (gameSystem.TryAddGameplayDebugRandomQuest(out var quest))
```

### `TryAssignTalent` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:562: gameSystem.TryAssignTalent(selectedTalent.TalentInstanceId, slot.SlotId, out var result);
```

### `TryBirthPrince` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Inheritance.cs:338: var born = gameSystem.TryBirthPrince(string.Empty, out var prince);
```

### `TryClaimExpeditionRewards` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:508: if (!gameSystem.TryClaimExpeditionRewards(expedition.ExpeditionId, out var result))
```

### `TryClaimQuestRewards` (2)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Quest.cs:315: gameSystem.TryClaimQuestRewards(quest);
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanelHUD_Quest.cs:392: gameSystem.TryClaimQuestRewards(currentQuest);
```

### `TryRecruitTalentOffer` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:508: gameSystem.TryRecruitTalentOffer(offer.OfferId, out var result);
```

### `TryRefreshTalents` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:496: gameSystem.TryRefreshTalents(out var result);
```

### `TryStartExpedition` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Expedition.cs:485: if (!gameSystem.TryStartExpedition(destination, ParsePopulation(), supplies, out var result))
```

### `TrySubmitQuestResources` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Quest.cs:319: gameSystem.TrySubmitQuestResources(quest);
```

### `TryUnassignTalent` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:544: gameSystem.TryUnassignTalent(talent.TalentInstanceId, out var result);
```

### `TryUnassignTalentSlot` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:574: gameSystem.TryUnassignTalentSlot(slot.SlotId, out var result);
```

### `TryUpgradeTalent` (1)

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Talent.cs:532: gameSystem.TryUpgradeTalent(talent.TalentInstanceId, out var result);
```

### `Turn` (3)

```text
Assets/Landsong/Scripts/QuestSystem/QuestService.cs:49: private TurnService Turn => context.Turn;
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_HUD.cs:273: turn = gameSystem == null ? null : gameSystem.Turn;
Assets/Landsong/Scripts/UI/UIPanel_Game/BuildingResourceFloatTextManager.cs:89: SetTurnService(gameSystem == null ? null : gameSystem.Turn);
```

### `UnlockBuildingBlueprint` (3)

```text
Assets/Landsong/Scripts/InheritanceSystem/RoyalInheritanceService.cs:1343: var unlocked = context.UnlockBuildingBlueprint(building.Definition.BuildingId);
Assets/Landsong/Scripts/TechnologySystem/TechnologyEffects.cs:82: var unlocked = context.UnlockBuildingBlueprint(definition.BuildingId);
Assets/Landsong/Scripts/TalentSystem/TalentService.cs:1303: var unlocked = context.UnlockBuildingBlueprint(building.Definition.BuildingId);
```

### `UnregisterBuilding` (1)

```text
Assets/Landsong/Scripts/BuildingSystem/BuildingBase.cs:633: gameSystem.UnregisterBuilding(this);
```

## Duplicate DataManager save aliases

### `SaveGameData`

```text
```

### `OverwriteSaveGameData`

```text
```

### `SaveNewGameData`

```text
Assets/Landsong/Scripts/UI/UIPanel_SaveGame/UIPanel_SaveConfirmation.cs:125: DataManager.Instance.SaveNewGameData();
```

### `QuickSaveGameData`

```text
Assets/Landsong/Scripts/UI/UIPanel_Game/GamePanel_Pause.cs:63: DataManager.Instance.QuickSaveGameData();
```

### `AutoSaveGameData`

```text
```

### `GetAllGameDataMeta`

```text
Assets/Landsong/Scripts/UI/UIPanel_SaveGame/UIPanel_LoadGame.cs:90: IReadOnlyList<GameDataMeta> metas = DataManager.Instance.GetAllGameDataMeta();
```
