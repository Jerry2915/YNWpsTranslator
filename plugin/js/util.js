var YNTranslator = window.YNTranslator || {};

YNTranslator.HELPER_URL = "http://127.0.0.1:17653";

YNTranslator.getUrlPath = function () {
  var url = decodeURI(document.location.toString());
  var index = url.lastIndexOf("/");
  return index >= 0 ? url.substring(0, index) : url;
};

YNTranslator.showMessage = function (message) {
  alert(message);
};

YNTranslator.hasChinese = function (text) {
  return /[\u3400-\u9fff]/.test(text || "");
};

// 是否值得翻译：含中文或拉丁字母（英文/印尼语）即翻译；
// 纯数字、日期、符号、空白会被跳过。源语种由模型自动识别。
YNTranslator.isTranslatable = function (text) {
  var t = text || "";
  return /[\u3400-\u9fff]/.test(t) || /[A-Za-z]/.test(t);
};

YNTranslator.isAlreadyBilingual = function (text) {
  var lines = String(text || "").split(/\r?\n/);
  if (lines.length < 2) {
    return false;
  }

  var chineseFound = false;
  var indonesianFound = false;
  for (var i = 0; i < lines.length; i++) {
    if (YNTranslator.hasChinese(lines[i])) {
      chineseFound = true;
    } else if (/[A-Za-z]/.test(lines[i])) {
      indonesianFound = true;
    }
  }
  return chineseFound && indonesianFound;
};

YNTranslator.safeText = function (value) {
  return value === null || typeof value === "undefined" ? "" : String(value);
};

YNTranslator.setStatus = function (message) {
  try {
    window.Application.StatusBar = message || false;
  } catch (e) {
    // Status bar updates are optional on older WPS builds.
  }
};
