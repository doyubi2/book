using System;
using System.Collections.Generic;
using System.Windows.Forms; // Windows Forms 네임스페이스 추가
using System.Linq; // Enumerable 사용을 위해 추가
using ScottPlot; // ScottPlot 네임스페이스 추가
using System.Data.SQLite;
using SDL2;
using static SDL2.SDL;

namespace AccountBook
{
    public class AccountBook
    {
        private SQLiteConnection connection;
        private IntPtr window;
        private IntPtr renderer;
        private TextRenderer textRenderer;
        private List<InputField> inputFields;
        private int activeFieldIndex = -1;
        private bool isRunning = true;
        private const int WINDOW_WIDTH = 800;
        private const int WINDOW_HEIGHT = 600;
        private bool isViewingTransactions = false;
        private bool isViewingMonthlySummary = false;
        private bool isMainView = true;
        private int selectedTransactionId = -1;
        private string currentSortOrder = "Date DESC";

        public void Initialize()
        {
            InitializeSDL();
            InitializeDatabase();

            // 입력 필드 초기화
            inputFields = new List<InputField>
            {
                new InputField(270, 130, 200, 30, textRenderer, InputField.InputType.Number), // 금액
                new InputField(270, 178, 200, 30, textRenderer, InputField.InputType.Selection), // 분류
                new InputField(270, 228, 200, 30, textRenderer), // 카테고리
                new InputField(270, 278, 200, 30, textRenderer)  // 설명
            };
        }

        private void InitializeSDL()
        {
            if (SDL_Init(SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine($"SDL 초기화 실패: {SDL_GetError()}");
                return;
            }

            window = SDL_CreateWindow("가계부 프로그램",
                SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
                WINDOW_WIDTH, WINDOW_HEIGHT,
                SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (window == IntPtr.Zero)
            {
                Console.WriteLine($"윈도우 생성 실패: {SDL_GetError()}");
                return;
            }

            renderer = SDL_CreateRenderer(window, -1,
                SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
                SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            if (renderer == IntPtr.Zero)
            {
                Console.WriteLine($"렌더러 생성 실패: {SDL_GetError()}");
                return;
            }

            textRenderer = new TextRenderer(renderer);
            SDL_StartTextInput();
        }

        private void InitializeDatabase()
        {
            connection = new SQLiteConnection("Data Source=accountbook.db;Version=3;");
            connection.Open();
            CreateDatabase();
        }

        private void CreateDatabase()
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Transactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    Amount DECIMAL NOT NULL,
                    Category TEXT NOT NULL,
                    Description TEXT,
                    Type TEXT NOT NULL
                )";
                command.ExecuteNonQuery();
            }
        }

        public void Run()
        {
            while (isRunning)
            {
                HandleEvents();
                Render();
                SDL_Delay(16);
            }
            Cleanup();
        }

        private void HandleEvents()
        {
            while (SDL_PollEvent(out SDL_Event e) != 0)
            {
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT:
                        isRunning = false;
                        break;
                    case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                        HandleMouseClick(e.button.x, e.button.y);
                        break;
                    case SDL_EventType.SDL_KEYDOWN:
                        HandleKeyPress(e.key.keysym.sym);
                        break;
                    case SDL_EventType.SDL_TEXTINPUT:
                        if (activeFieldIndex >= 0)
                        {
                            inputFields[activeFieldIndex].HandleInput(e, activeFieldIndex, inputFields);
                        }
                        break;
                }
            }
        }
        private void HandleKeyPress(SDL_Keycode key)
        {
            switch (key)
            {
                case SDL_Keycode.SDLK_RETURN:
                    if (activeFieldIndex >= 0)
                    {
                        SaveTransaction();
                    }
                    break;
                case SDL_Keycode.SDLK_2: // 거래 내역 보기 토글
                    isViewingTransactions = !isViewingTransactions;
                    isViewingMonthlySummary = false; // 월별 보기 중단
                    isMainView = !isViewingTransactions;
                    break;
                case SDL_Keycode.SDLK_3: // 월별 내역 보기 토글
                    isViewingMonthlySummary = !isViewingMonthlySummary;
                    isViewingTransactions = false; // 거래 내역 보기 중단
                    isMainView = !isViewingMonthlySummary;
                    break;
                case SDL_Keycode.SDLK_ESCAPE:
                    if (!isMainView)
                    {
                        isMainView = true;
                        isViewingTransactions = false;
                        isViewingMonthlySummary = false; // 모든 비주 메인 상태 종료
                        selectedTransactionId = -1;
                    }
                    else
                    {
                        isRunning = false;
                    }
                    break;
                case SDL_Keycode.SDLK_DELETE:
                    if (isViewingTransactions && selectedTransactionId != -1)
                    {
                        DeleteTransaction(selectedTransactionId);
                        ViewTransactions();
                    }
                    break;
                case SDL_Keycode.SDLK_d:
                    if (isViewingTransactions)
                    {
                        currentSortOrder = "Date DESC";
                        ViewTransactions();
                    }
                    break;
                case SDL_Keycode.SDLK_a:
                    if (isViewingTransactions)
                    {
                        currentSortOrder = "Amount DESC";
                        ViewTransactions();
                    }
                    break;
                case SDL_Keycode.SDLK_g: // 그래프 보기
                    ShowGraph();
                    break;
            }
        }

        private void DeleteTransaction(int id)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM Transactions WHERE Id = @id";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        private void HandleMouseClick(int x, int y)
        {
            if (isViewingTransactions)
            {
                // 거래 내역 행 클릭 처리
                int startY = 120; // 첫 번째 행의 Y 좌표
                int rowHeight = 40;
                int maxRows = 10;

                for (int i = 0; i < maxRows; i++)
                {
                    int rowY = startY + (i * rowHeight);
                    if (y >= rowY && y < rowY + rowHeight && x >= 50 && x <= 750)
                    {
                        // 현재 선택된 행의 ID를 저장
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT Id FROM Transactions ORDER BY Date DESC LIMIT 1 OFFSET @index";
                            command.Parameters.AddWithValue("@index", i);
                            var result = command.ExecuteScalar();
                            if (result != null)
                            {
                                selectedTransactionId = Convert.ToInt32(result);
                                ViewTransactions();
                            }
                        }
                        break;
                              }
                }
            }
            else
            {
                for (int i = 0; i < inputFields.Count; i++)
                {
                    if (inputFields[i].Contains(x, y))
                    {
                        SetActiveField(i);
                        break;
                    }
                }
            }
        }

                        private void SetActiveField(int index)
        {
            for (int i = 0; i < inputFields.Count; i++)
            {
                inputFields[i].SetActive(i == index);
            }
            activeFieldIndex = index;
        }

        private void Render()
        {
            SDL_SetRenderDrawColor(renderer, 220, 220, 200, 205);
            SDL_RenderClear(renderer);

            if (isViewingTransactions)
            {
                ViewTransactions();
            }
            else if (isViewingMonthlySummary) // 월별 내역 보기
            {
                ViewMonthlySummary();
            }
            else
            {
                RenderMainView();
            }

            SDL_RenderPresent(renderer);
        }


        private void RenderMainView()
        {
            DrawMenuBox(150, -10, 500, 500);

            SDL_Color titleColor = new SDL_Color { r = 50, g = 50, b = 50, a = 255 };
            textRenderer.RenderText("=== 가계부 프로그램 ===", 230, 50, "large", titleColor);

            // 입력 필드 레이블
            SDL_Color textColor = new SDL_Color { r = 0, g = 0, b = 0, a = 205 };
            textRenderer.RenderText("금액:", 270, 100, "large", textColor);
            textRenderer.RenderText("분류 (수입/지출):", 270, 150, "large", textColor);
            textRenderer.RenderText("카테고리:", 270, 200, "large", textColor);
            textRenderer.RenderText("설명:", 270, 250, "large", textColor);

            foreach (var field in inputFields)
            {
                field.Render(renderer);
            }
        }
        private void ViewMonthlySummary()
        {
            SDL_SetRenderDrawColor(renderer, 240, 240, 240, 255);
            SDL_RenderClear(renderer);

            SDL_Color headerColor = new SDL_Color { r = 50, g = 50, b = 50, a = 255 };
            textRenderer.RenderText("=== 월별 수입/지출 내역 ===", 200, 40, "large", headerColor);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
            SELECT 
                strftime('%Y-%m', Date) as Month,
                SUM(CASE WHEN Type = '수입' THEN Amount ELSE 0 END) as Income,
                SUM(CASE WHEN Type = '지출' THEN Amount ELSE 0 END) as Expense
            FROM Transactions 
            GROUP BY strftime('%Y-%m', Date)
            ORDER BY Month DESC";

                int yPos = 120;
                SDL_Color textColor = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string month = reader.GetString(0);
                        decimal income = reader.GetDecimal(1);
                        decimal expense = reader.GetDecimal(2);
                        decimal balance = income - expense;

                        textRenderer.RenderText($"월: {month}", 50, yPos, "medium", textColor);
                        textRenderer.RenderText($"수입: {income:N0}원", 250, yPos, "medium", textColor);
                        textRenderer.RenderText($"지출: {expense:N0}원", 450, yPos, "medium", textColor);
                        textRenderer.RenderText($"잔액: {balance:N0}원", 650, yPos, "medium", textColor);

                        yPos += 40;
                    }
                }
            }

            SDL_RenderPresent(renderer);
        }

        private void ViewTransactions()
        {
            SDL_SetRenderDrawColor(renderer, 240, 240, 240, 255);
            SDL_RenderClear(renderer);

            SDL_Color headerColor = new SDL_Color { r = 50, g = 50, b = 50, a = 255 };
            textRenderer.RenderText("=== 거래 내역 ===", 270, 10, "large", headerColor);

            int startY = 100;
            int columnWidth = 150;
            var headers = new[] { "날짜", "금액", "분류", "카테고리", "설명" };

            for (int i = 0; i < headers.Length; i++)
            {
                textRenderer.RenderText(headers[i], 50 + columnWidth * i, startY, "medium", headerColor);
            }

            // 도움말 텍스트 추가
            textRenderer.RenderText("Del: 선택한 항목 삭제", 30, WINDOW_HEIGHT - 30, "small", headerColor);
            textRenderer.RenderText("D: 날짜순 정렬", 200, WINDOW_HEIGHT - 30, "small", headerColor);
            textRenderer.RenderText("A: 금액순 정렬", 340, WINDOW_HEIGHT - 30, "small", headerColor);

            try
            {
                LoadTransactions(startY + 40);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"거래 내역을 불러오는 중 오류 발생: {ex.Message}");
            }
        }
        private void LoadTransactions(int startY)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT * FROM Transactions ORDER BY {currentSortOrder}";

                int yPos = startY;
                int columnWidth = 150;
                SDL_Color textColor = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);  // ID 저장
                        if (id == selectedTransactionId)
                        {
                            // 선택된 항목 하이라이트
                            SDL_SetRenderDrawColor(renderer, 200, 200, 255, 255);
                            SDL_Rect highlightRect = new SDL_Rect
                            {
                                x = 45,
                                y = yPos - 5,
                                w = 700,
                                h = 30
                            };
                            SDL_RenderFillRect(renderer, ref highlightRect);
                        }

                        textRenderer.RenderText(reader.GetString(1), 50, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetDecimal(2).ToString("N0"), 50 + columnWidth, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetString(5), 50 + columnWidth * 2, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetString(3), 50 + columnWidth * 3, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetString(4), 50 + columnWidth * 4, yPos, "small", textColor);

                        yPos += 40;
                    }
                }
            }
        }

        private void LoadTransactions(int startY, string orderBy = "Date DESC")
        {
            const int columnWidth = 150;
            int yPos = startY;
            string[] headers = { "날짜", "금액", "분류", "카테고리", "설명" };

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT * FROM Transactions ORDER BY {orderBy} LIMIT 10";

                using (var reader = command.ExecuteReader())
                {
                    int rowIndex = 0;
                    while (reader.Read())
                    {
                        DateTime date = DateTime.Parse(reader.GetString(1));
                        string formattedDate = date.ToString("yyyy-MM-dd");

                        if (rowIndex % 2 == 0)
                        {
                            SDL_SetRenderDrawColor(renderer, 230, 230, 230, 255);
                        }
                        else
                        {
                            SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                        }

                        SDL_Rect rowRect = new SDL_Rect { x = 50, y = yPos - 10, w = columnWidth * headers.Length, h = 40 };
                        SDL_RenderFillRect(renderer, ref rowRect);
                        SDL_Color textColor = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };

                        textRenderer.RenderText(formattedDate, 50, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetDecimal(2).ToString("N0"), 50 + columnWidth, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetString(5), 50 + columnWidth * 2, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetString(3), 50 + columnWidth * 3, yPos, "small", textColor);
                        textRenderer.RenderText(reader.GetString(4), 50 + columnWidth * 4, yPos, "small", textColor);

                        yPos += 40;
                        rowIndex++;
                    }
                }
            }
        }

        private void DrawMenuBox(int x, int y, int w, int h)
        {
            SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            SDL_Rect menuBox = new SDL_Rect { x = x, y = y, w = w, h = h };
            SDL_RenderFillRect(renderer, ref menuBox);

            SDL_SetRenderDrawColor(renderer, 200, 200, 200, 255);
            SDL_RenderDrawRect(renderer, ref menuBox);
        }

        private void SaveTransaction()
        {
            if (!ValidateFields()) return;

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                INSERT INTO Transactions 
                (Date, Amount, Type, Category, Description)
                VALUES 
                (@date, @amount, @type, @category, @description)";

                    command.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                    command.Parameters.AddWithValue("@amount", decimal.Parse(inputFields[0].GetText()));
                    command.Parameters.AddWithValue("@type", inputFields[1].GetText());
                    command.Parameters.AddWithValue("@category", inputFields[2].GetText());
                    command.Parameters.AddWithValue("@description", inputFields[3].GetText());

                    command.ExecuteNonQuery();
                    Console.WriteLine("거래 내역이 저장되었습니다.");
                }

                ClearInputFields();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"거래 내역 저장 중 오류 발생: {ex.Message}");
            }
        }



        private void ShowGraph()
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
            SELECT strftime('%Y-%m', Date) as Month,
            SUM(CASE WHEN Type = '수입' THEN Amount ELSE 0 END) as Income,
            SUM(CASE WHEN Type = '지출' THEN Amount ELSE 0 END) as Expense
            FROM Transactions 
            GROUP BY Month
            ORDER BY Month";

                var months = new List<string>();
                var incomes = new List<double>();
                var expenses = new List<double>();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        months.Add(reader.GetString(0));
                        incomes.Add(Convert.ToDouble(reader.GetDecimal(1)));
                        expenses.Add(Convert.ToDouble(reader.GetDecimal(2)));
                    }
                }

                var form = new Form();
                form.Size = new System.Drawing.Size(800, 600);

                var formsPlot = new ScottPlot.WinForms.FormsPlot();
                formsPlot.Dock = DockStyle.Fill;
                form.Controls.Add(formsPlot);

                var plt = formsPlot.Plot;

                // 막대 그래프 데이터 추가 (최신 ScottPlot 메서드)
                var bar1 = plt.Add.Bars(incomes.ToArray());
                var bar2 = plt.Add.Bars(expenses.ToArray());

                bar1.Label = "수입";
                bar2.Label = "지출";

                // X축 눈금 설정 (최신 ScottPlot 메서드)
                plt.XAxis.Label = "월";
                plt.XAxis.TickGenerator = new ScottPlot.NamedTicks(months.ToArray());

                plt.Title("월별 수입/지출 현황");
                plt.ShowLegend();

                formsPlot.Refresh();
                form.ShowDialog();
            }
        }

        private bool ValidateFields()
        {
            return !string.IsNullOrEmpty(inputFields[0].GetText()) &&
                   !string.IsNullOrEmpty(inputFields[1].GetText()) &&
                   !string.IsNullOrEmpty(inputFields[2].GetText());
        }

        private void ClearInputFields()
        {
            foreach (var field in inputFields)
            {
                field.Clear();
            }
        }

        private void Cleanup()
        {
            SDL_StopTextInput();
            textRenderer?.Cleanup();
            connection?.Dispose();
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }
}