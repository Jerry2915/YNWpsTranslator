function OnAddinLoad(ribbonUI) {
  if (typeof window.Application.ribbonUI !== "object") {
    window.Application.ribbonUI = ribbonUI;
  }
  return true;
}

function OnAction(control) {
  switch (control.Id) {
    case "btnTranslateSelection":
      YNTranslator.translateScope("selection");
      break;
    case "btnTranslateSheet":
      YNTranslator.translateScope("sheet");
      break;
    case "btnTranslateWorkbook":
      if (confirm("将翻译当前工作簿中的所有工作表。继续吗？")) {
        YNTranslator.translateScope("workbook");
      }
      break;
    case "btnApiSettings":
      window.Application.ShowDialog(
        YNTranslator.getUrlPath() + "/ui/settings.html",
        "翻译 API 设置",
        620 * window.devicePixelRatio,
        650 * window.devicePixelRatio,
        false
      );
      break;
    case "btnGlossary":
      window.Application.ShowDialog(
        YNTranslator.getUrlPath() + "/ui/glossary.html",
        "专业术语表",
        760 * window.devicePixelRatio,
        650 * window.devicePixelRatio,
        false
      );
      break;
    case "btnAbout":
      alert("RBI表格翻译助手 v1.0.0\n———————————————\n· 中 / 英 / 印尼语互译，源语种自动识别\n· 内置专业术语表，支持自定义译法\n· 仅支持 WPS 表格\n\nDeveloped by RBI IE · Jerry\nhttps://github.com/Jerry2915/YNWpsTranslator");
      break;
  }
  return true;
}

function GetImage(control) {
  switch (control.Id) {
    case "btnTranslateSelection":
      return "images/selection.svg";
    case "btnTranslateSheet":
      return "images/sheet.svg";
    case "btnTranslateWorkbook":
      return "images/workbook.svg";
    case "btnApiSettings":
      return "images/settings.svg";
    case "btnGlossary":
      return "images/glossary.svg";
    default:
      return "images/info.svg";
  }
}
