(function () {
  var entries = document.getElementById("entries");
  var langLabel = document.getElementById("langLabel");
  var saveButton = document.getElementById("saveButton");
  var status = document.getElementById("status");

  function setStatus(message, isError) {
    status.textContent = message || "";
    status.className = isError ? "status error" : "status";
  }

  YNTranslator.request("GET", "/glossary").then(function (response) {
    entries.value = response.text || "";
    langLabel.textContent = response.lang || "印尼语";
  }).catch(function (error) {
    setStatus(error.message, true);
  });

  saveButton.onclick = function () {
    saveButton.disabled = true;
    YNTranslator.request("POST", "/glossary", { text: entries.value }).then(function (response) {
      langLabel.textContent = response.lang || langLabel.textContent;
      setStatus("已保存「" + (response.lang || "") + "」术语表，共 " + response.count + " 条。");
    }).catch(function (error) {
      setStatus(error.message, true);
    }).finally(function () {
      saveButton.disabled = false;
    });
  };

  document.getElementById("closeButton").onclick = function () {
    window.close();
  };
})();
