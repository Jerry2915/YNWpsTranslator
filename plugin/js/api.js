var YNTranslator = window.YNTranslator || {};

YNTranslator.request = function (method, path, body) {
  return new Promise(function (resolve, reject) {
    var xhr;
    try {
      xhr = window.WpsInvoke && WpsInvoke.CreateXHR
        ? WpsInvoke.CreateXHR()
        : new XMLHttpRequest();
      xhr.open(method, YNTranslator.HELPER_URL + path, true);
      xhr.setRequestHeader("Content-Type", "application/json; charset=utf-8");
      xhr.timeout = 120000;
      xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) {
          return;
        }
        var payload = null;
        try {
          payload = xhr.responseText ? JSON.parse(xhr.responseText) : {};
        } catch (e) {
          payload = { ok: false, error: xhr.responseText || "本机翻译助手返回了无法识别的数据。" };
        }
        if (xhr.status >= 200 && xhr.status < 300 && payload.ok !== false) {
          resolve(payload);
        } else {
          reject(new Error(payload.error || ("请求失败，HTTP " + xhr.status)));
        }
      };
      xhr.onerror = function () {
        reject(new Error("无法连接本机翻译助手，请重新安装或启动插件。"));
      };
      xhr.ontimeout = function () {
        reject(new Error("翻译请求超时，请稍后重试。"));
      };
      xhr.send(body ? JSON.stringify(body) : null);
    } catch (e) {
      reject(e);
    }
  });
};

YNTranslator.ensureHelper = function () {
  return YNTranslator.request("GET", "/health").catch(function () {
    throw new Error("本机翻译助手未运行。请从开始菜单打开“YN WPS Translator > Start Translator Helper”，然后重试。");
  });
};

YNTranslator.translateTexts = function (texts) {
  return YNTranslator.request("POST", "/translate", { texts: texts });
};
