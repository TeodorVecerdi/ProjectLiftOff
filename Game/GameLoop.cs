using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GXPEngine;
using GXPEngine.Core;

namespace Game {
    public class GameLoop : GameObject {
        public int TilesVertical => Settings.World.Depth + Settings.World.TopOffset;
        public readonly int TilesHorizontal;
        public int Score;

        private float gravityTimeLeft = Settings.GravityFrequency;
        private float timeSinceLastMovement;
        private float drillTimeLeft;
        private float drillTimeOriginal;
        private float drillSpeed = 1f;
        private float cameraVelocity;

        private bool startedDrilling;
        private bool canStartDrilling;
        private bool isDrillOn;

        private ObjectType[,] tiles;
        private ObjectType[,] tilesBackground;
        private Texture2D topBackground;
        private Canvas HUD;
        private Camera camera;
        private Player player;
        private FuelBar fuelBar;
        private FuelStation fuelStation;
        private VisibilitySystem visibility;
        private DrillProgressIndicator drillProgressIndicator;
        private Vector2Int lastDrillDirection = Vector2Int.zero;
        private GameManager gameManager;

        private Sound ambientSound;

        public ObjectType[,] Tiles => tiles;

        public GameLoop(GameManager gameManager) {
            this.gameManager = gameManager;
            TilesHorizontal = (int) (Globals.WIDTH / Globals.TILE_SIZE);
            tiles = new ObjectType[TilesHorizontal, Settings.World.TopOffset + Settings.World.Depth];
            tilesBackground = new ObjectType[TilesHorizontal, Settings.World.TopOffset + Settings.World.Depth];
            SoundManager.Instance.Play("ambient");
            GenerateWorld(out var playerSpawnLocation);
            InitializeSceneObjects(playerSpawnLocation);
        }

        private void Update() {
            DrawHud();

            var (playerX, playerY) = new Vector2(player.x, player.y).ToGrid().ToInt().Unpack();
            var movementDirection = new Vector2Int((int) Input.GetAxisDown("Horizontal"), (int) Input.GetAxisDown("Vertical"));
            var drillDirection = new Vector2Int((int) Input.GetAxis("Horizontal"), (int) Input.GetAxis("Vertical"));

            // Constrain movement to only one axis, with priority for vertical movement
            if (movementDirection.x != 0 && movementDirection.y != 0) {
                movementDirection.x = 0;
            }

            if (drillDirection.x != 0 && drillDirection.y != 0) {
                drillDirection.x = 0;
            }

            var desiredPosition = movementDirection.Add(playerX, playerY);
            var desiredDrillDirection = drillDirection.Add(playerX, playerY);
            var rangeCheck = desiredPosition.x >= 0 && desiredPosition.x < TilesHorizontal && desiredPosition.y >= 0 && desiredPosition.y < TilesVertical;
            var movedThisFrame = false;

            UpdateDrilling(ref playerX, ref playerY, ref rangeCheck, ref movedThisFrame, ref movementDirection, ref desiredPosition, ref drillDirection, ref desiredDrillDirection);
            UpdateMovement(ref playerX, ref playerY, ref rangeCheck, ref movedThisFrame, ref movementDirection, ref desiredPosition);
            UpdateGravity(ref playerX, ref playerY, ref rangeCheck, ref movementDirection, ref desiredPosition);
            UpdateFuel(ref playerX, ref playerY);
            UpdateTimers();
            UpdateCamera();
        }

        private void DrawHud() {
            HUD.graphics.Clear(Color.Empty);
            HUD.graphics.DrawString("SCORE: " + Score, FontLoader.Instance[64f], Brushes.FloralWhite, Globals.WIDTH / 2f, 24, FontLoader.CenterAlignment);
            HUD.graphics.DrawString($"DEPTH: {Settings.World.BlockSize * (player.y / Globals.TILE_SIZE - Settings.World.TopOffset + 1)}m", FontLoader.Instance[32f], Brushes.AntiqueWhite, Globals.WIDTH / 2f, 64, FontLoader.CenterAlignment);
            HUD.graphics.DrawString("FUEL", FontLoader.Instance[64f], Brushes.FloralWhite, Globals.WIDTH - 30, Globals.HEIGHT / 2f, FontLoader.CenterVerticalAlignment);
            HUD.graphics.DrawString("FPS: " + game.currentFps, SystemFonts.StatusFont, Brushes.DarkRed, 0, 8, FontLoader.LeftAlignment);
        }

        private void InitializeSceneObjects(int playerSpawnLocation) {
            drillProgressIndicator = new DrillProgressIndicator {Alpha = 0};
            fuelStation = new FuelStation("data/fuel_station.png", 3, Settings.World.TopOffset - 1, Settings.InitialFuelRefills);
            fuelStation.Move(0, 2 * Globals.TILE_SIZE);

            player = new Player();
            player.SetXY(playerSpawnLocation * Globals.TILE_SIZE, (Settings.World.TopOffset - 1) * Globals.TILE_SIZE);

            camera = new Camera(0, 0, Globals.WIDTH, Globals.HEIGHT) {x = (int) (Globals.WIDTH / 2f)}; // weird camera behaviour fix

            fuelBar = new FuelBar();
            fuelBar.Move(-Globals.WIDTH / 2f, 0f); // weird camera behaviour fix
            HUD = new Canvas(Globals.WIDTH, Globals.HEIGHT, false);
            HUD.Move(0, -Globals.HEIGHT / 2f);
            HUD.Move(-Globals.WIDTH / 2f, 0f); // weird camera behaviour fix

            visibility = new VisibilitySystem(player);
            topBackground = Texture2D.GetInstance("data/background_above_ground_2ver3.png", true);

            /*topBackground = new Sprite("data/background_test.jpg", true, false);
            topBackground.Move(0, -2*Globals.TILE_SIZE);
            topBackground.SetScaleXY(0.711458333f);*/

            camera.AddChild(fuelBar);
            camera.LateAddChild(HUD);

            AddChild(fuelStation);
            AddChild(visibility);
            AddChild(player);
            AddChild(drillProgressIndicator);
            AddChild(camera);
        }

        private void UpdateDrilling(ref int playerX, ref int playerY, ref bool rangeCheck, ref bool movedThisFrame, ref Vector2Int movementDirection, ref Vector2Int desiredPosition, ref Vector2Int drillDirection, ref Vector2Int desiredDrillDirection) {
            if (Input.GetButtonDown("Drill")) {
                isDrillOn = !isDrillOn;
                player.AnimationState = isDrillOn ? AnimationState.DrillOn : AnimationState.Idle;
            }

            if (!isDrillOn || !rangeCheck) return;

            if (!canStartDrilling && movementDirection != Vector2Int.zero) {
                canStartDrilling = true;
            }

            var wantsToDrill = drillDirection != Vector2Int.zero;
            var isDrillingUp = drillDirection.y == -1;
            var hasGroundUnder = playerY + 1 == TilesVertical || tiles[playerX, playerY + 1] != ObjectType.Empty;
            if (canStartDrilling && tiles[desiredDrillDirection.x, desiredDrillDirection.y] == ObjectType.Stone) {
                SoundManager.Instance.Play("stoneHit");
                canStartDrilling = false;
            }

            if (canStartDrilling && wantsToDrill && !isDrillingUp && hasGroundUnder && Settings.Tiles.TypeToTile[tiles[desiredDrillDirection.x, desiredDrillDirection.y]].Drillable) {
                if (lastDrillDirection != drillDirection || !startedDrilling) {
                    SoundManager.Instance.Play("drilling");
                    drillTimeOriginal = drillSpeed * Settings.Tiles.TypeToTile[tiles[desiredDrillDirection.x, desiredDrillDirection.y]].TimeToDrill;
                    drillTimeLeft = drillTimeOriginal;
                }

                player.AnimationState = AnimationState.Drilling;
                drillProgressIndicator.visible = true;
                drillProgressIndicator.SetXY(desiredDrillDirection.x * Globals.TILE_SIZE, desiredDrillDirection.y * Globals.TILE_SIZE);
                startedDrilling = true;
            } else {
                if (startedDrilling)
                    SoundManager.Instance.Stop("drilling");
                player.AnimationState = AnimationState.DrillOn;
                drillProgressIndicator.Alpha = 0f;
                drillProgressIndicator.visible = false;
                startedDrilling = false;
                canStartDrilling = false;
            }

            // BREAK TILE IF TIME IS DONE
            if (startedDrilling && drillTimeLeft <= 0) {
                Score += Settings.Tiles.TypeToTile[tiles[desiredDrillDirection.x, desiredDrillDirection.y]].ScoreAmount;
                fuelBar.FuelAmount += Settings.Tiles.TypeToTile[tiles[desiredDrillDirection.x, desiredDrillDirection.y]].FuelAmount;
                player.Move(drillDirection.ToWorld().ToVec2());
                tiles[playerX, playerY] = ObjectType.Empty;
                var minedTile = tiles[desiredDrillDirection.x, desiredDrillDirection.y];
                tiles[desiredDrillDirection.x, desiredDrillDirection.y] = ObjectType.Player;
                movedThisFrame = true;
                
                player.AnimationState = AnimationState.DrillOn;
                drillProgressIndicator.Alpha = 0f;
                drillProgressIndicator.visible = false;
                startedDrilling = false;
                canStartDrilling = false;

                // Do something if a pickup was mined
                if (Settings.World.UpgradeTypes.Contains(minedTile)) {
                    // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                    switch (minedTile) {
                        case ObjectType.DrillingSpeedUpgrade: {
                            drillSpeed *= Settings.DrillSpeedUpgradeMultiplier;
                            break;
                        }
                        case ObjectType.ViewDistanceUpgrade: {
                            visibility.scale *= Settings.ViewDistanceUpgradeMultiplier;
                            break;
                        }
                        case ObjectType.FuelCapacityUpgrade: {
                            var oldFuelCapacity = fuelBar.FuelCapacity;
                            fuelBar.FuelCapacity *= Settings.FuelCapacityUpgradeMultiplier;
                            fuelBar.FuelAmount += (fuelBar.FuelCapacity - oldFuelCapacity);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException(nameof(minedTile), $"Mined tile {minedTile} is not an upgrade type");
                    }
                }

                // Recalculate position
                (playerX, playerY) = new Vector2(player.x, player.y).ToGrid().ToInt().Unpack();
                desiredPosition = movementDirection.Add(playerX, playerY);
                rangeCheck = desiredPosition.x >= 0 && desiredPosition.x < TilesHorizontal && desiredPosition.y >= 0 && desiredPosition.y < TilesVertical;
            }

            lastDrillDirection = drillDirection;
        }

        private void UpdateMovement(ref int playerX, ref int playerY, ref bool rangeCheck, ref bool movedThisFrame, ref Vector2Int movementDirection, ref Vector2Int desiredPosition) {
            if (movementDirection == Vector2Int.zero) return;

            // If player can move
            if (rangeCheck && tiles[desiredPosition.x, desiredPosition.y] == ObjectType.Empty && !movedThisFrame) {
                // Do the actual movement
                player.Move(movementDirection.ToWorld().ToVec2());
                tiles[playerX, playerY] = ObjectType.Empty;
                tiles[desiredPosition.x, desiredPosition.y] = ObjectType.Player;

                // Recalculate position
                (playerX, playerY) = new Vector2(player.x, player.y).ToGrid().ToInt().Unpack();
                desiredPosition = movementDirection.Add(playerX, playerY);
                rangeCheck = desiredPosition.x >= 0 && desiredPosition.x < TilesHorizontal && desiredPosition.y >= 0 && desiredPosition.y < TilesVertical;
            }

            timeSinceLastMovement = 0f;
            gravityTimeLeft = Settings.GravityFrequency;
        }

        private void UpdateGravity(ref int playerX, ref int playerY, ref bool rangeCheck, ref Vector2Int movementDirection, ref Vector2Int desiredPosition) {
            if (!(timeSinceLastMovement > Settings.PlayerMovementThreshold) || !(gravityTimeLeft <= 0))
                return;
            if (playerY + 1 < TilesVertical && tiles[playerX, playerY + 1] == ObjectType.Empty) {
                player.Move(0, Globals.TILE_SIZE);
                tiles[playerX, playerY] = ObjectType.Empty;
                tiles[playerX, playerY + 1] = ObjectType.Player;

                // Recalculate position
                (playerX, playerY) = new Vector2(player.x, player.y).ToGrid().ToInt().Unpack();
                desiredPosition = movementDirection.Add(playerX, playerY);
                rangeCheck = desiredPosition.x >= 0 && desiredPosition.x < TilesHorizontal && desiredPosition.y >= 0 && desiredPosition.y < TilesVertical;
            }

            gravityTimeLeft = Settings.GravityFrequency;
        }

        private void UpdateFuel(ref int playerX, ref int playerY) {
            if (fuelStation.IsPlayerOnRefillPoint(playerX, playerY) && Input.GetButtonDown("Refuel") && fuelStation.CanPlayerRefill()) {
                fuelBar.Refuel();
                fuelStation.ReduceRefillsLeft();
            }

            fuelBar.ChangeFuel(Settings.IdleFuelConsumption * Time.deltaTime);
            if (isDrillOn) fuelBar.ChangeFuel(Settings.DrillOnFuelConsumption * Time.deltaTime);
            if (startedDrilling) fuelBar.ChangeFuel(Settings.DrillingFuelConsumption * Time.deltaTime);
            
            if (fuelBar.FuelAmount <= 0) {
                gameManager.ShouldStopPlaying = true;
            }
        }

        private void UpdateTimers() {
            timeSinceLastMovement += Time.deltaTime;
            if (timeSinceLastMovement > Settings.PlayerMovementThreshold) {
                gravityTimeLeft -= Time.deltaTime;
            }

            if (startedDrilling) {
                drillTimeLeft -= Time.deltaTime;
                drillProgressIndicator.Alpha = Math.Map(drillTimeLeft, drillTimeOriginal, 0f, 0f, 1f);
            }
        }

        private void UpdateCamera() {
            camera.y = Mathf.SmoothDamp(camera.y, player.y, ref cameraVelocity, 0.3f);
            camera.y = Mathf.Clamp(camera.y, Globals.HEIGHT / 2f - Globals.TILE_SIZE * 2, (Settings.World.Depth + Settings.World.TopOffset) * Globals.TILE_SIZE - Globals.HEIGHT / 2f);
        }

        public void DrawTileGrid(GLContext glContext) {
            var playerY = (int) (player.y / Globals.TILE_SIZE);
            var startY = Mathf.Max(playerY - Settings.RenderDistance, 0);
            var endY = Mathf.Min(playerY + Settings.RenderDistance, TilesVertical - 1);
            for (var i = 0; i < TilesHorizontal; i++) {
                for (var j = startY; j <= endY; j++) {
                    float[] verts = {i * Globals.TILE_SIZE, j * Globals.TILE_SIZE, i * Globals.TILE_SIZE + Globals.TILE_SIZE, j * Globals.TILE_SIZE, i * Globals.TILE_SIZE + Globals.TILE_SIZE, j * Globals.TILE_SIZE + Globals.TILE_SIZE, i * Globals.TILE_SIZE, j * Globals.TILE_SIZE + Globals.TILE_SIZE};
                    Settings.Tiles.TypeToTile[tilesBackground[i, j]].Texture.Bind();
                    glContext.DrawQuad(verts, Globals.QUAD_UV);
                    if(tiles[i, j] == ObjectType.Player) continue;
                    Settings.Tiles.TypeToTile[tiles[i, j]].Texture.Bind();
                    glContext.DrawQuad(verts, Globals.QUAD_UV);
                }
            }
        }

        protected override void RenderSelf(GLContext glContext) {
            glContext.SetColor(0xff, 0xff, 0xff, 0xff);
            topBackground.Bind();
            glContext.DrawQuad(topBackground.TextureVertices(1, offset: new Vector2(0, -2 * Globals.TILE_SIZE)), Globals.QUAD_UV);
            fuelStation.Draw(glContext);
            DrawTileGrid(glContext);
            drillProgressIndicator.Draw(glContext);
            player.Draw(glContext);
            visibility.Draw(glContext);
        }
        private void GenerateWorld(out int playerSpawnLocation) {
            for (var x = 0; x < TilesHorizontal; x++) {
                for (var y = 0; y < Settings.World.Depth; y++) {
                    var gridY = y + Settings.World.TopOffset;
                    var hardness = y > Settings.World.HardDirtStartDepth ? 2 : y > Settings.World.MediumDirtStartDepth ? 1 : 0;

                    tilesBackground[x, gridY] = ObjectType.Background;
                    var spawnTypeChance = Rand.Value;
                    if (spawnTypeChance <= Settings.World.StoneChance) {
                        tiles[x, gridY] = Settings.Tiles.TileToHardness[ObjectType.Stone][hardness];
                    } else if (spawnTypeChance <= Settings.World.StoneChance + Settings.World.OreChance) {
                        var weightedRandomizer = new WeightedRandomizer();
                        foreach (var oreType in Settings.World.OreDepthSpawning.Keys) {
                            try {
                                var oreBracket = Settings.World.OreDepthSpawning[oreType].First(value => y.BetweenInclusive(value.FromY, value.ToY));
                                if (oreBracket != null)
                                    weightedRandomizer.AddChance(oreType, oreBracket.Chance);
                            } catch (InvalidOperationException) {
                                // Debug.LogError($"Could not find matching element for ore type: {oreType} at y-depth: {y}");
                            }
                        }

                        var tileToSpawn = weightedRandomizer.GetValue();
                        tiles[x, gridY] = Settings.Tiles.TileToHardness[tileToSpawn][hardness];
                    } else {
                        tiles[x, gridY] = Settings.Tiles.TileToHardness[ObjectType.Dirt][hardness];
                    }
                }
            }

            // Spawn pickups / upgrades
            var upgradeSpawnLocations = new List<(int, int, ObjectType)>();
            for (var i = 0; i < Settings.World.UpgradeCount; i++) {
                var (upgradeX, upgradeY) = (Rand.Range(0, TilesHorizontal), Rand.Range(Settings.World.TopOffset + 1, TilesVertical));
                var upgradeType = Settings.World.UpgradeTypes[Rand.Range(0, Settings.World.UpgradeTypes.Count)];
                upgradeSpawnLocations.Add((upgradeX, upgradeY, upgradeType));
            }

            upgradeSpawnLocations.ForEach(spawnLocation => {
                var (upgradeX, upgradeY, upgradeType) = spawnLocation;
                tiles[upgradeX, upgradeY] = upgradeType;
            });

            // Make FuelStation ground stone 
            for (var x = 0; x < 4; x++) {
                tiles[x, Settings.World.TopOffset] = ObjectType.Stone;
            }

            playerSpawnLocation = Rand.Range(6, TilesHorizontal - 1);
            tiles[playerSpawnLocation, Settings.World.TopOffset - 1] = ObjectType.Player;
        } 
    }
}