(() => {
  function aceToggleComment(event) {
    if (!(event.ctrlKey || event.metaKey)) return true;
    if (event.key !== "/" && event.code !== "Slash") return true;

    const textarea = event.target;
    if (!textarea || textarea.tagName !== "TEXTAREA") return true;

    event.preventDefault();

    const value = textarea.value;
    let selStart = textarea.selectionStart;
    let selEnd = textarea.selectionEnd;

    if (selEnd > 0 && value[selEnd - 1] === "\n") {
      selEnd -= 1;
    }

    const lineStart = value.lastIndexOf("\n", selStart - 1) + 1;
    let lineEnd = value.indexOf("\n", selEnd);
    if (lineEnd === -1) lineEnd = value.length;

    const block = value.slice(lineStart, lineEnd);
    const lines = block.split("\n");
    const nonEmpty = lines.filter((line) => line.trim().length > 0);
    const allCommented =
      nonEmpty.length > 0 && nonEmpty.every((line) => /^\s*\/\//.test(line));

    const updatedLines = lines.map((line) => {
      if (line.trim().length === 0) return line;
      if (allCommented) {
        return line.replace(/^(\s*)\/\/ ?/, "$1");
      }
      return line.replace(/^(\s*)/, "$1//");
    });

    const newBlock = updatedLines.join("\n");
    textarea.value = value.slice(0, lineStart) + newBlock + value.slice(lineEnd);
    textarea.selectionStart = lineStart;
    textarea.selectionEnd = lineStart + newBlock.length;
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
    return false;
  }

  window.aceToggleComment = aceToggleComment;
})();
