(function () {
  var apiKey = document.getElementById("apiKey");
  var model = document.getElementById("model");
  var targetLang = document.getElementById("targetLang");
  var baseUrl = document.getElementById("baseUrl");
  var intervalMs = document.getElementById("intervalMs");
  var replaceExistingBilingual = document.getElementById("replaceExistingBilingual");
  var createBackup = document.getElementById("createBackup");
  var saveButton = document.getElementById("saveButton");
  var status = document.getElementById("status");

  function setStatus(message, isError) {
    status.textContent = message || "";
    status.className = isError ? "status error" : "status";
  }

  function setBusy(busy) {
    saveButton.disabled = busy;
  }

  YNTranslator.request("GET", "/settings").then(function (response) {
    model.value = response.model || "glm-4-flash";
    targetLang.value = response.targetLang || "印尼语";
    baseUrl.value = response.baseUrl || "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    intervalMs.value = response.intervalMs || 300;
    replaceExistingBilingual.checked = response.replaceExistingBilingual !== false;
    createBackup.checked = response.createBackup !== false;
    if (response.hasKey) {
      apiKey.placeholder = "已保存 API Key，留空表示不修改";
    }
  }).catch(function (error) {
    setStatus(error.message, true);
  });

  saveButton.onclick = function () {
    setBusy(true);
    setStatus("正在保存并测试...");
    YNTranslator.request("POST", "/settings", {
      apiKey: apiKey.value,
      model: model.value.trim(),
      targetLang: targetLang.value.trim(),
      baseUrl: baseUrl.value.trim(),
      intervalMs: Number(intervalMs.value) || 300,
      replaceExistingBilingual: replaceExistingBilingual.checked,
      createBackup: createBackup.checked
    }).then(function () {
      return YNTranslator.request("POST", "/test", {});
    }).then(function (response) {
      apiKey.value = "";
      apiKey.placeholder = "已保存 API Key，留空表示不修改";
      setStatus("连接成功。测试：上岗培训 → " + response.translation);
    }).catch(function (error) {
      setStatus(error.message, true);
    }).finally(function () {
      setBusy(false);
    });
  };

  document.getElementById("closeButton").onclick = function () {
    window.close();
  };
})();
