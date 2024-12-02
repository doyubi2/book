using System;
using System.Collections.Generic;
using SDL2;

namespace AccountBook
{
    public class TextRenderer
    {
        private IntPtr renderer;
        private Dictionary<string, IntPtr> fonts;

        public TextRenderer(IntPtr renderer)
        {
            this.renderer = renderer;
            fonts = new Dictionary<string, IntPtr>();
            InitializeFonts();
        }

        private void InitializeFonts()
        {
            if (SDL_ttf.TTF_Init() < 0)
            {
                throw new Exception($"SDL_ttf 초기화 실패: {SDL.SDL_GetError()}");
            }

            fonts["large"] = SDL_ttf.TTF_OpenFont("NanumGothic.ttf", 32);
            fonts["medium"] = SDL_ttf.TTF_OpenFont("NanumGothic.ttf", 24);
            fonts["small"] = SDL_ttf.TTF_OpenFont("NanumGothic.ttf", 18);

            foreach (var font in fonts.Values)
            {
                if (font == IntPtr.Zero)
                {
                    throw new Exception($"폰트 로드 실패: {SDL.SDL_GetError()}");
                }
            }
        }

        public IntPtr GetFont(string fontType = "medium")
        {
            return fonts[fontType];
        }

        public void RenderText(string text, int x, int y, string fontType = "medium", SDL.SDL_Color? color = null)
        {
            if (string.IsNullOrEmpty(text)) return;

            var textColor = color ?? new SDL.SDL_Color { r = 0, g = 0, b = 0, a = 255 };
            var font = fonts[fontType];

            IntPtr surface = SDL_ttf.TTF_RenderUTF8_Blended(font, text, textColor);
            if (surface == IntPtr.Zero)
            {
                Console.WriteLine($"텍스트 렌더링 실패: {SDL.SDL_GetError()}");
                return;
            }

            IntPtr texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            SDL.SDL_FreeSurface(surface);

            if (texture == IntPtr.Zero)
            {
                Console.WriteLine($"텍스처 생성 실패: {SDL.SDL_GetError()}");
                return;
            }

            SDL.SDL_QueryTexture(texture, out uint format, out int access, out int width, out int height);
            SDL.SDL_Rect destRect = new SDL.SDL_Rect { x = x, y = y, w = width, h = height };
            SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref destRect);

            SDL.SDL_DestroyTexture(texture);
        }

        public void Cleanup()
        {
            foreach (var font in fonts.Values)
            {
                if (font != IntPtr.Zero)
                {
                    SDL_ttf.TTF_CloseFont(font);
                }
            }
            fonts.Clear();
            SDL_ttf.TTF_Quit();
        }
    }
}