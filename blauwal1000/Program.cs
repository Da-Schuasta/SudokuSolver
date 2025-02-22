using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Namespace-Deklaration
namespace SudokuSolver
{
    class Program
    {
        private static readonly HttpClient httpClient;
        private static readonly string JWT = Environment.GetEnvironmentVariable("JWT_TOKEN") ?? "67b71073d3b60cde98e8dba3___eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJlbWFpbCI6ImFiZW5kZXNzZW5AZmgtc2FsemJ1cmcuYWMuYXQiLCJpYXQiOjE3NDAxMTkxNjcsImV4cCI6MTc0MDU1MTE2N30.Gq44ntRt7Jq3hiAwoa3tpnLRZi9paLAISUfBNv4EC2c";
        private static readonly string IP = Environment.GetEnvironmentVariable("SUDOKU_SERVER_IP") ?? "https://193.170.119.74";

        // Static constructor to initialize HttpClient with SSL bypass
        static Program()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            httpClient = new HttpClient(handler);
        }

        static async Task Main(string[] args)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime.AddDays(3);

            while (DateTime.Now < endTime)
            {
                try
                {
                    await ProcessSudokusAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
        }

        private static async Task ProcessSudokusAsync()
        {
            var sudokus = await FetchSudokusAsync();
            if (sudokus == null) return;

            var solutions = new { data = new List<Solution>() };

            var tasks = sudokus.Select(async sudoku =>
            {
                var solvedBoard = Solve(sudoku.masked);
                solutions.data.Add(new Solution { id = sudoku.id, template = solvedBoard });
            });

            await Task.WhenAll(tasks);

            var result = await SubmitSolutionsAsync(solutions);
            if (result != null)
            {
                Console.WriteLine($"Submission result: {JsonSerializer.Serialize(result)}");
            }
        }

        private static bool SolveSudoku(int[][] board)
        {
            int[] rows = new int[9];
            int[] cols = new int[9];
            int[] boxes = new int[9];

            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (board[i][j] != 0)
                    {
                        int mask = 1 << (board[i][j] - 1);
                        rows[i] |= mask;
                        cols[j] |= mask;
                        boxes[(i / 3) * 3 + (j / 3)] |= mask;
                    }
                }
            }

            (int, int) GetNextCell()
            {
                int minCandidates = int.MaxValue;
                (int, int) nextCell = (-1, -1);
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        if (board[i][j] == 0)
                        {
                            int boxIndex = (i / 3) * 3 + (j / 3);
                            int possible = ~(rows[i] | cols[j] | boxes[boxIndex]) & 0x1FF;
                            int numCandidates = BitCount(possible);
                            if (numCandidates < minCandidates)
                            {
                                minCandidates = numCandidates;
                                nextCell = (i, j);
                            }
                        }
                    }
                }
                return nextCell;
            }

            bool Backtrack()
            {
                var (i, j) = GetNextCell();
                if (i == -1 && j == -1) return true;

                int boxIndex = (i / 3) * 3 + (j / 3);
                int possible = ~(rows[i] | cols[j] | boxes[boxIndex]) & 0x1FF;
                while (possible != 0)
                {
                    int num = (possible & -possible).BitLength();
                    int mask = 1 << (num - 1);
                    rows[i] |= mask;
                    cols[j] |= mask;
                    boxes[boxIndex] |= mask;
                    board[i][j] = num;

                    if (Backtrack()) return true;

                    rows[i] ^= mask;
                    cols[j] ^= mask;
                    boxes[boxIndex] ^= mask;
                    board[i][j] = 0;

                    possible ^= mask;
                }
                return false;
            }

            return Backtrack();
        }

        private static int[][] Solve(int[][] currentSudoku)
        {
            int[][] board = currentSudoku.Select(row => row.ToArray()).ToArray();
            if (SolveSudoku(board))
            {
                return board;
            }
            return currentSudoku;
        }

        private static async Task<List<Sudoku>> FetchSudokusAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{IP}/sudokus");
                request.Headers.Add("Cookie", $"auth={JWT}");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Sudoku>>(json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to fetch Sudokus: {e.Message}");
                return null;
            }
        }

        private static async Task<object> SubmitSolutionsAsync(object solutions)
        {
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(solutions), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{IP}/validate-sudokus")
                {
                    Content = content
                };
                request.Headers.Add("Cookie", $"auth={JWT}");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to submit solutions: {e.Message}");
                return null;
            }
        }

        private static int BitCount(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }

        private class Sudoku
        {
            public int id { get; set; }
            public int[][] masked { get; set; }
        }

        private class Solution
        {
            public int id { get; set; }
            public int[][] template { get; set; }
        }
    }

    // Statische Klasse für die Erweiterungsmethode auf Namespace-Ebene
    public static class IntExtensions
    {
        public static int BitLength(this int value)
        {
            int length = 0;
            while (value != 0)
            {
                length++;
                value >>= 1;
            }
            return length;
        }
    }
}