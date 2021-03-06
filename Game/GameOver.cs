﻿using GXPEngine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using GXPEngine.Core;

namespace Game {
    public class GameOver : GameObject {
        private int score;
        private Texture2D backgroundTexture;
        private Canvas canvas;

        public GameOver(int score) {
            canvas = new Canvas(Globals.WIDTH, Globals.HEIGHT);
            this.score = score;
            backgroundTexture = Texture2D.GetInstance("data/loseGameOver.png");
            AddChild(canvas);
        }

        private void Update() {
            canvas.graphics.Clear(Color.Transparent);
            canvas.graphics.DrawString("Game Over", FontLoader.Instance[128f], Brushes.White, Globals.WIDTH / 2f, 64f, FontLoader.CenterAlignment);
            canvas.graphics.DrawString($"Score", FontLoader.Instance[64f], Brushes.White, Globals.WIDTH/2f,128f+32f, FontLoader.CenterAlignment);
            canvas.graphics.DrawString($"{score}", FontLoader.Instance[64f], Brushes.White, Globals.WIDTH/2f,128f+64f+32f, FontLoader.CenterAlignment);
            canvas.graphics.DrawString($"press any button", FontLoader.Instance[48f], Brushes.White, Globals.WIDTH/2f,Globals.HEIGHT - 48f, FontLoader.CenterAlignment);
            if (Input.GetAxisDown("Horizontal") != 0 || Input.GetAxisDown("Vertical") != 0 || Input.GetButtonDown("Drill") || Input.GetButtonDown("Refuel")) {
                GameManager.Instance.ShouldShowMenu = true;
            }
        }

        protected override void RenderSelf(GLContext glContext) {
            glContext.SetColor(0xff,0xff,0xff,0xff);
            backgroundTexture.Bind();
            var verts = backgroundTexture.TextureVertices();
            glContext.DrawQuad(verts, Globals.QUAD_UV);
        }
    }
}