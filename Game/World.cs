using GXPEngine;
using GXPEngine.Core;

namespace Game {
    public class World : GameObject {
        public int VerticalTiles = 400;
        private TileGrid grid;
        private Sprite topBackground;
        private Sprite fuelStation;
     
        public World() {
            topBackground = new Sprite("data/background_test.jpg", true, false);
            topBackground.Move(0, -2*Globals.TILE_SIZE);
            topBackground.SetScaleXY(0.711458333f);
            fuelStation = new Sprite("data/fuel_station.png", true, false);
            fuelStation.Move(0, 2 * Globals.TILE_SIZE);
            grid = new TileGrid(VerticalTiles);
            
            AddChild(topBackground);
            AddChild(fuelStation);
            AddChild(grid);
        }
    }

}