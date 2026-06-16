var YNTranslator = window.YNTranslator || {};

YNTranslator.translationRunning = false;

YNTranslator.getRangeCells = function (range) {
  var cells = [];
  if (!range) {
    return cells;
  }

  var areas = range.Areas;
  var areaCount = areas ? areas.Count : 1;
  for (var a = 1; a <= areaCount; a++) {
    var area = areas ? areas.Item(a) : range;
    var count = area.Cells.Count;
    for (var i = 1; i <= count; i++) {
      cells.push(area.Cells.Item(i));
    }
  }
  return cells;
};

YNTranslator.getTranslatableSource = function (cell, options) {
  try {
    if (cell.HasFormula === true) {
      return null;
    }
    if (cell.MergeCells === true) {
      var mergeArea = cell.MergeArea;
      if (cell.Row !== mergeArea.Row || cell.Column !== mergeArea.Column) {
        return null;
      }
    }
    var value = cell.Value2;
    if (typeof value !== "string") {
      return null;
    }
    var text = value.trim();
    if (!text || !YNTranslator.isTranslatable(text)) {
      return null;
    }
    if (!YNTranslator.isAlreadyBilingual(text)) {
      return text;
    }
    if (!options.replaceExistingBilingual) {
      return null;
    }
    // 已是“原文+译文”双语：取第一行（原文）重新翻译。
    var firstLine = text.split(/\r?\n/)[0];
    return (firstLine || "").trim() || null;
  } catch (e) {
    return null;
  }
};

YNTranslator.collectFromRange = function (range, sheetName, options) {
  var result = [];
  var cells;
  try {
    var constants = range.SpecialCells(2, 2);
    cells = YNTranslator.getRangeCells(constants);
  } catch (e) {
    cells = YNTranslator.getRangeCells(range);
  }

  for (var i = 0; i < cells.length; i++) {
    var cell = cells[i];
    var source = YNTranslator.getTranslatableSource(cell, options);
    if (!source) {
      continue;
    }
    result.push({
      cell: cell,
      source: source,
      sheetName: sheetName,
      address: cell.Address(false, false),
      initialRowHeight: Number(cell.EntireRow.RowHeight) || 0,
      merged: cell.MergeCells === true
    });
  }
  return result;
};

YNTranslator.collectTargets = function (scope, options) {
  var app = window.Application;
  var workbook = app.ActiveWorkbook;
  if (!workbook) {
    throw new Error("当前没有打开 WPS 表格文件。");
  }

  var targets = [];
  if (scope === "selection") {
    var selection = app.Selection;
    if (!selection || !selection.Cells) {
      throw new Error("请先选择需要翻译的单元格。");
    }
    return YNTranslator.collectFromRange(selection, app.ActiveSheet.Name, options);
  }

  if (scope === "sheet") {
    return YNTranslator.collectFromRange(app.ActiveSheet.UsedRange, app.ActiveSheet.Name, options);
  }

  var sheets = workbook.Worksheets;
  for (var i = 1; i <= sheets.Count; i++) {
    var sheet = sheets.Item(i);
    targets = targets.concat(YNTranslator.collectFromRange(sheet.UsedRange, sheet.Name, options));
  }
  return targets;
};

YNTranslator.makeBackup = function () {
  var workbook = window.Application.ActiveWorkbook;
  if (!workbook || !workbook.Path || workbook.ReadOnly) {
    return null;
  }
  var fullName = workbook.FullName;
  var dot = fullName.lastIndexOf(".");
  var stamp = new Date().toISOString().replace(/[-:T]/g, "").substring(0, 14);
  var backup = dot > 0
    ? fullName.substring(0, dot) + "_翻译前备份_" + stamp + fullName.substring(dot)
    : fullName + "_翻译前备份_" + stamp;
  workbook.SaveCopyAs(backup);
  return backup;
};

YNTranslator.applyResults = function (targets, translations) {
  var changedRows = {};
  var changed = 0;
  for (var i = 0; i < targets.length; i++) {
    var translated = translations[targets[i].source];
    if (!translated) {
      continue;
    }
    var cell = targets[i].cell;
    cell.Value2 = targets[i].source + "\n" + translated;
    cell.WrapText = true;
    var key = targets[i].sheetName + ":" + cell.Row;
    if (!changedRows[key]) {
      changedRows[key] = {
        row: cell.EntireRow,
        initialHeight: targets[i].initialRowHeight,
        hasMergedCell: targets[i].merged
      };
    } else {
      changedRows[key].initialHeight = Math.max(
        changedRows[key].initialHeight,
        targets[i].initialRowHeight);
      changedRows[key].hasMergedCell =
        changedRows[key].hasMergedCell || targets[i].merged;
    }
    changed++;
  }

  for (var rowKey in changedRows) {
    if (!changedRows.hasOwnProperty(rowKey)) {
      continue;
    }
    var rowInfo = changedRows[rowKey];
    var row = rowInfo.row;
    try {
      var oldHeight = Number(row.RowHeight) || 0;
      row.AutoFit();
      var minimumHeight = Math.max(oldHeight, rowInfo.initialHeight);
      if (rowInfo.hasMergedCell && rowInfo.initialHeight > 0) {
        minimumHeight = Math.min(409, rowInfo.initialHeight * 2);
      }
      if (Number(row.RowHeight) < minimumHeight) {
        row.RowHeight = minimumHeight;
      }
    } catch (e) {
      if (rowInfo.hasMergedCell && rowInfo.initialHeight > 0) {
        row.RowHeight = Math.min(409, rowInfo.initialHeight * 2);
      }
    }
  }
  return changed;
};

YNTranslator.translateScope = async function (scope) {
  if (YNTranslator.translationRunning) {
    YNTranslator.showMessage("已有翻译任务正在运行，请稍候。");
    return;
  }

  YNTranslator.translationRunning = true;
  var app = window.Application;
  var oldScreenUpdating = true;
  var backup = null;
  try {
    await YNTranslator.ensureHelper();
    var userSettings = await YNTranslator.request("GET", "/settings");
    var targets = YNTranslator.collectTargets(scope, userSettings);
    if (!targets.length) {
      YNTranslator.showMessage("没有找到需要翻译的文本。公式、数值和日期会自动跳过。");
      return;
    }

    var unique = [];
    var seen = {};
    for (var i = 0; i < targets.length; i++) {
      if (!seen[targets[i].source]) {
        seen[targets[i].source] = true;
        unique.push(targets[i].source);
      }
    }

    YNTranslator.setStatus("正在调用智谱翻译，共 " + unique.length + " 条唯一文本...");
    var response = await YNTranslator.translateTexts(unique);
    if (userSettings.createBackup !== false) {
      backup = YNTranslator.makeBackup();
    }
    oldScreenUpdating = app.ScreenUpdating;
    app.ScreenUpdating = false;
    var changed = YNTranslator.applyResults(targets, response.translations || {});
    app.ScreenUpdating = oldScreenUpdating;
    var message = "翻译完成，共更新 " + changed + " 个单元格。";
    if (backup) {
      message += "\n已创建翻译前备份：\n" + backup;
    }
    if (response.failedCount) {
      message += "\n有 " + response.failedCount + " 条未翻译，请检查网络、API 配额或术语。";
    }
    YNTranslator.showMessage(message);
  } catch (e) {
    try {
      app.ScreenUpdating = oldScreenUpdating;
    } catch (restoreError) {
    }
    YNTranslator.showMessage("翻译失败：\n" + (e.message || e));
  } finally {
    YNTranslator.setStatus(false);
    YNTranslator.translationRunning = false;
  }
};
