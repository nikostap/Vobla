const chatRoot = document.querySelector("[data-conversation-id]");

if (chatRoot) {
  const conversationId = chatRoot.dataset.conversationId;
  const userId = chatRoot.dataset.userId;
  const list = chatRoot.querySelector("[data-message-list]");
  const form = chatRoot.querySelector("[data-message-form]");
  const input = form?.querySelector("textarea");
  const fileInput = form?.querySelector('input[type="file"]');
  const selectedFiles = form?.querySelector("[data-selected-files]");
  const typing = chatRoot.querySelector("[data-typing]");
  const presence = document.querySelector("[data-presence]");
  const presenceDot = presence?.closest(".chat-person")?.querySelector(".presence-dot");
  const separator = "\u001e";
  let socket;
  let invocationId = 0;
  let reconnectDelay = 500;
  let typingTimer;

  const send = (target, args, expectsResult = false) => {
    if (socket?.readyState !== WebSocket.OPEN) return false;
    socket.send(JSON.stringify({ type: 1, ...(expectsResult ? { invocationId: String(invocationId++) } : {}), target, arguments: args }) + separator);
    return true;
  };

  const appendMessage = (message) => {
    if (list?.querySelector(`[data-message-id="${message.id}"]`)) return;
    const item = document.createElement("li");
    item.dataset.messageId = message.id;
    if (String(message.senderId) === userId) item.className = "mine";
    const meta = document.createElement("small");
    meta.textContent = `${message.senderName} · ${new Date(message.createdAt).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" })}`;
    const body = document.createElement("p"); body.textContent = message.text;
    item.append(meta, body);
    if (item.classList.contains("mine")) { const receipt = document.createElement("em"); receipt.textContent = "Доставлено"; item.append(receipt); }
    list?.append(item); item.scrollIntoView({ block: "end" });
    send("MarkRead", [conversationId]);
  };

  const handle = (packet) => {
    if (packet.type !== 1) return;
    if (packet.target === "ReceiveMessage") appendMessage(packet.arguments[0]);
    if (packet.target === "TypingChanged" && typing) { typing.textContent = packet.arguments[1] ? "Собеседник печатает…" : ""; }
    if (packet.target === "PresenceChanged" && presence && String(packet.arguments[0]) !== userId) {
      const online = Boolean(packet.arguments[1]);
      presence.textContent = online ? "в сети" : "не в сети";
      presenceDot?.classList.toggle("is-online", online);
    }
    if (packet.target === "ReadReceipt") document.querySelectorAll(".message-list li.mine em").forEach((item) => item.textContent = "Прочитано");
  };

  async function connect() {
    try {
      const negotiated = await fetch("/hubs/chat/negotiate?negotiateVersion=1", { method: "POST", headers: { "Content-Type": "application/json" } });
      if (!negotiated.ok) throw new Error("negotiate failed");
      const { connectionToken } = await negotiated.json();
      const protocol = location.protocol === "https:" ? "wss:" : "ws:";
      socket = new WebSocket(`${protocol}//${location.host}/hubs/chat?id=${encodeURIComponent(connectionToken)}`);
      socket.addEventListener("open", () => socket.send(JSON.stringify({ protocol: "json", version: 1 }) + separator));
      socket.addEventListener("message", ({ data }) => {
        for (const raw of data.split(separator).filter(Boolean)) {
          const packet = JSON.parse(raw);
          if (packet.type === undefined) { reconnectDelay = 500; send("JoinConversation", [conversationId], true); }
          else handle(packet);
        }
      });
      socket.addEventListener("close", () => { setTimeout(connect, reconnectDelay); reconnectDelay = Math.min(10_000, reconnectDelay * 2); });
    } catch { setTimeout(connect, reconnectDelay); reconnectDelay = Math.min(10_000, reconnectDelay * 2); }
  }

  form?.addEventListener("submit", (event) => {
    const text = input?.value.trim();
    if (fileInput?.files.length) return;
    if (!text) { event.preventDefault(); return; }
    if (socket?.readyState !== WebSocket.OPEN) return;
    event.preventDefault(); send("SendMessage", [conversationId, text], true); input.value = ""; send("Typing", [conversationId, false]);
  });
  input?.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      form?.requestSubmit();
    }
  });
  input?.addEventListener("input", () => { send("Typing", [conversationId, true]); clearTimeout(typingTimer); typingTimer = setTimeout(() => send("Typing", [conversationId, false]), 1200); });
  fileInput?.addEventListener("change", () => {
    if (!selectedFiles) return;
    const names = [...fileInput.files].map((file) => file.name);
    selectedFiles.textContent = names.length ? `Выбрано: ${names.join(", ")} · нажмите «Отправить»` : "Ctrl+Enter — отправить · до 3 файлов по 20 МБ";
    selectedFiles.classList.toggle("has-files", names.length > 0);
  });
  document.querySelectorAll("[data-template]").forEach((button) => button.addEventListener("click", () => { if (input) { input.value = button.dataset.template; input.focus(); } }));

  const imageViewer = document.querySelector("[data-chat-image-viewer]");
  const viewerImages = imageViewer ? [...imageViewer.querySelectorAll("[data-chat-viewer-image]")] : [];
  const imageCounter = imageViewer?.querySelector("[data-chat-image-counter]");
  let imageIndex = 0;
  const showImage = (index) => {
    if (!viewerImages.length) return;
    imageIndex = (index + viewerImages.length) % viewerImages.length;
    viewerImages.forEach((image, currentIndex) => {
      const active = currentIndex === imageIndex;
      image.classList.toggle("is-active", active);
      image.setAttribute("aria-hidden", String(!active));
    });
    if (imageCounter) imageCounter.textContent = `${imageIndex + 1} / ${viewerImages.length}`;
  };
  document.querySelectorAll("[data-chat-image-open]").forEach((button) => button.addEventListener("click", () => {
    showImage(Number(button.dataset.galleryIndex));
    imageViewer?.showModal();
  }));
  imageViewer?.querySelector("[data-chat-image-close]")?.addEventListener("click", () => imageViewer.close());
  imageViewer?.querySelector("[data-chat-image-prev]")?.addEventListener("click", () => showImage(imageIndex - 1));
  imageViewer?.querySelector("[data-chat-image-next]")?.addEventListener("click", () => showImage(imageIndex + 1));
  imageViewer?.addEventListener("click", (event) => { if (event.target === imageViewer) imageViewer.close(); });
  imageViewer?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowLeft") { event.preventDefault(); showImage(imageIndex - 1); }
    if (event.key === "ArrowRight") { event.preventDefault(); showImage(imageIndex + 1); }
  });
  connect();
}
