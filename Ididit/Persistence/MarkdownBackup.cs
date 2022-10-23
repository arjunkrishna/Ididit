﻿using Ididit.App.Data;
using Ididit.Data;
using Ididit.Data.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ididit.Persistence;

internal class MarkdownBackup
{
    private readonly JsInterop _jsInterop;
    private readonly IRepository _repository;

    public MarkdownBackup(JsInterop jsInterop, IRepository repository)
    {
        _jsInterop = jsInterop;
        _repository = repository;
    }

    public async Task ImportData(Stream stream)
    {
        using StreamReader streamReader = new(stream);

        string text = await streamReader.ReadToEndAsync();

        CategoryModel? category = null;
        GoalModel? goal = null;
        TaskModel? task = null;

        foreach (string line in text.Split(Environment.NewLine))
        {
            if (line.StartsWith("#"))
            {
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] != '#')
                        break;

                    if (i < line.Length - 2 && line[i + 1] == ' ')
                    {
                        string name = line[(i + 2)..];

                        if (i == 0)
                        {
                            category = _repository.CreateCategory(name);

                            await _repository.AddCategory(category);
                        }
                        else if (category is not null)
                        {
                            category = category.CreateCategory(_repository.NextCategoryId, name);

                            await _repository.AddCategory(category);
                        }
                    }
                }
            }
            else if (line.StartsWith("**") && line.EndsWith("**"))
            {
                if (category is not null)
                {
                    goal = category.CreateGoal(_repository.NextGoalId, line);
                    await _repository.AddGoal(goal);
                }
            }
            else if (goal is not null)
            {
                goal.Details += string.IsNullOrEmpty(goal.Details) ? line : Environment.NewLine + line;

                if (task != null && line.StartsWith("- "))
                {
                    task.DetailsText += line;

                    task.AddDetail(line);

                    await _repository.UpdateTask(task.Id);
                }
                else
                {
                    task = goal.CreateTask(_repository.NextTaskId, line);

                    await _repository.AddTask(task);
                }
            }
        }
    }

    public async Task ExportData(IDataModel data)
    {
        StringBuilder stringBuilder = new();

        await SaveCategoryList(data.CategoryList, stringBuilder, level: 1);

        string md = stringBuilder.ToString();

        await _jsInterop.SaveAsUTF8("ididit.md", md);
    }

    private async Task SaveCategoryList(List<CategoryModel> categoryList, StringBuilder stringBuilder, int level)
    {
        foreach (CategoryModel category in categoryList)
        {
            stringBuilder.AppendLine($"{new string('#', Math.Min(level, 6))} {category.Name}");
            stringBuilder.AppendLine();

            foreach (GoalModel goal in category.GoalList)
            {
                stringBuilder.AppendLine($"**{goal.Name}**");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(goal.Details.Replace(Environment.NewLine, $"  {Environment.NewLine}"));
                stringBuilder.AppendLine();
            }

            if (category.CategoryList.Any())
            {
                await SaveCategoryList(category.CategoryList, stringBuilder, level + 1);
            }
        }
    }
}
