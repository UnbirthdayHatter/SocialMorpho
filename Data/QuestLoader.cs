using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SocialMorpho.Data;

public static class QuestLoader
{
    public static List<QuestData> LoadFromJson(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new List<QuestData>();
            }

            var jsonContent = File.ReadAllText(filePath);
            var questFile = JsonSerializer.Deserialize<QuestFile>(jsonContent);

            return questFile?.Quests ?? new List<QuestData>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading quests from {filePath}: {ex.Message}");
            return new List<QuestData>();
        }
    }

    public static void CreateExampleQuestFile(string filePath)
    {
        try
        {
            var exampleQuests = new QuestFile
            {
                Quests = new List<QuestData>
                {
                    new QuestData
                    {
                        Id = 1001,
                        Title = "Weekly Social Gathering",
                        Description = "Attend 3 social events with FC members",
                        Type = QuestType.Social,
                        GoalCount = 3,
                        ResetSchedule = ResetSchedule.Weekly
                    },
                    new QuestData
                    {
                        Id = 1002,
                        Title = "Daily Buff Share",
                        Description = "Share buffs with 5 different players",
                        Type = QuestType.Buff,
                        GoalCount = 5,
                        ResetSchedule = ResetSchedule.Daily
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(exampleQuests, options);
            File.WriteAllText(filePath, jsonContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating example quest file: {ex.Message}");
        }
    }
}
