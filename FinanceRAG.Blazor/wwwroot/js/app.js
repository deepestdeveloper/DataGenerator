window.scrollToBottom = function () {
    const messageArea = document.getElementById('messageArea');
    if (messageArea) {
        messageArea.scrollTop = messageArea.scrollHeight;
    }
};
