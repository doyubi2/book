using System;
using System.Collections.Generic;
using SDL2;

namespace AccountBook
{
    public unsafe class InputField
    {
        private string text = "";
        private bool isActive = false;
        private int x, y, width, height;
        private TextRenderer textRenderer;
        private Dictionary<string, IntPtr> textureCache;
        private InputType inputType;
        private IntPtr renderer;

        public enum InputType
        {
            Number,
            Text,
            Selection
        }

        public InputField(int x, int y, int width, int height, TextRenderer textRenderer, InputType type = InputType.Text)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.textRenderer = textRenderer;
            this.inputType = type;
            textureCache = new Dictionary<string, IntPtr>();
        }

        public void HandleInput(SDL.SDL_Event e, int activeFieldIndex, List<InputField> inputFields)
        {
            if (!isActive) return;

            if (e.type == SDL.SDL_EventType.SDL_TEXTINPUT)
            {
                string input = new string((sbyte*)e.text.text);
                string newText = text + input;

                // 텍스트 너비 체크
                int textWidth;
                int textHeight;
                SDL_ttf.TTF_SizeText(textRenderer.GetFont(), newText, out textWidth, out textHeight);

                // 입력 필드 여백을 고려한 최대 너비
                int maxWidth = width - 10;

                // 텍스트가 입력 필드를 벗어나지 않고 유효한 입력일 때만 추가
                if (ValidateInput(newText) && textWidth < maxWidth)
                {
                    text = newText;
                }
            }
            else if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
            {
                if (e.key.keysym.sym == SDL.SDL_Keycode.SDLK_BACKSPACE && text.Length > 0)
                {
                    text = text.Substring(0, text.Length - 1);
                }
                else if (e.key.keysym.sym == SDL.SDL_Keycode.SDLK_TAB)
                {
                    int nextIndex = (activeFieldIndex + 1) % inputFields.Count;
                    foreach (var field in inputFields)
                    {
                        field.SetActive(false);
                    }
                    inputFields[nextIndex].SetActive(true);
                }
            }
        }

        private int MeasureTextWidth(string text, IntPtr font)
        {
            int textWidth, textHeight;

            if (SDL_ttf.TTF_SizeText(font, text, out textWidth, out textHeight) != 0)
            {
                Console.WriteLine($"텍스트 크기 측정 실패: {SDL.SDL_GetError()}");
                return 0; // 실패 시 안전하게 폭을 0으로 반환
            }

            return textWidth;
        }

        private void CacheTexture(string text)
        {
            if (!textureCache.ContainsKey(text) && textRenderer != null)
            {
                SDL.SDL_Color color = new SDL.SDL_Color { r = 0, g = 0, b = 0, a = 255 };
                textRenderer.RenderText(text, x + 5, y + 5);
            }
        }

        private bool ValidateInput(string input)
        {
            switch (inputType)
            {
                case InputType.Number:
                    return decimal.TryParse(input, out _);
                case InputType.Selection:
                    return input == "수입" || input == "지출";
                default:
                    return true;
            }
        }

        public void Render(IntPtr renderer)
        {
            this.renderer = renderer;

            // 입력 필드 배경
            SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            SDL.SDL_Rect rect = new SDL.SDL_Rect { x = x, y = y, w = width, h = height };
            SDL.SDL_RenderFillRect(renderer, ref rect);

            // 테두리
            SDL.SDL_SetRenderDrawColor(renderer, isActive ? (byte)0 : (byte)200, 0, 0, 255);
            SDL.SDL_RenderDrawRect(renderer, ref rect);

            // 텍스트 렌더링
            if (!string.IsNullOrEmpty(text))
            {
                if (textureCache.ContainsKey(text))
                {
                    // 캐시된 텍스처 사용
                    SDL.SDL_RenderCopy(renderer, textureCache[text], IntPtr.Zero, ref rect);
                }
                else
                {
                    // 새로운 텍스트 렌더링
                    textRenderer.RenderText(text, x + 5, y + 5);
                }
            }
        }

        public bool Contains(int mouseX, int mouseY)
        {
            return mouseX >= x && mouseX <= x + width &&
                   mouseY >= y && mouseY <= y + height;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }

        public string GetText()
        {
            return text;
        }

        public void Clear()
        {
            text = "";
            foreach (var texture in textureCache.Values)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            textureCache.Clear();
        }

        public void Cleanup()
        {
            foreach (var texture in textureCache.Values)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            textureCache.Clear();
        }
    }
}