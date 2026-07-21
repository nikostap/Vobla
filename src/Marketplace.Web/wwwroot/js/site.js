const catalogPage = document.querySelector(".catalog-page");
const map = document.querySelector(".marketplace-map-shell");
document.querySelector("[data-reload-page]")?.addEventListener("click", () => location.reload());
const cards = [];
const markers = [...document.querySelectorAll(".map-marker")];

const root = document.documentElement;
const themeMeta = document.querySelector('meta[name="theme-color"]');

function applyTheme(theme) {
  root.dataset.theme = theme;
  localStorage.setItem("marketplace-theme", theme);
  themeMeta?.setAttribute("content", theme === "dark" ? "#171614" : "#faf9f7");
  document.querySelectorAll(".theme-toggle").forEach((button) => {
    button.setAttribute("aria-pressed", String(theme === "dark"));
    button.setAttribute("aria-label", theme === "dark" ? "Включить светлую тему" : "Включить тёмную тему");
  });
}

applyTheme(root.dataset.theme || "light");

document.querySelectorAll(".theme-toggle").forEach((button) => {
  button.addEventListener("click", () => applyTheme(root.dataset.theme === "dark" ? "light" : "dark"));
});

const siteHeader = document.querySelector(".site-header");
let compactHeader = window.scrollY > 160;
let compactHeaderFrame = 0;

function updateCompactHeader() {
  compactHeaderFrame = 0;

  // Separate thresholds prevent the header's own height transition from
  // moving scrollY back across the same boundary and causing a toggle loop.
  if (!compactHeader && window.scrollY > 160) compactHeader = true;
  if (compactHeader && window.scrollY < 16) compactHeader = false;

  siteHeader?.toggleAttribute("data-compact", compactHeader);
}

window.addEventListener("scroll", () => {
  if (compactHeaderFrame) return;
  compactHeaderFrame = requestAnimationFrame(updateCompactHeader);
}, { passive: true });
updateCompactHeader();

function setHighlight(listingId, highlighted) {
  const card = cards.find((item) => item.dataset.listingId === listingId);
  const marker = markers.find((item) => item.dataset.listingId === listingId);
  card?.setAttribute("data-map-highlighted", String(highlighted));
  marker?.setAttribute("data-card-highlighted", String(highlighted));
}

function initializeListingCard(card) {
  if (card.dataset.cardReady === "true") return;
  card.dataset.cardReady = "true";
  cards.push(card);
  ["mouseenter", "focusin"].forEach((eventName) =>
    card.addEventListener(eventName, () => setHighlight(card.dataset.listingId, true)));
  ["mouseleave", "focusout"].forEach((eventName) =>
    card.addEventListener(eventName, () => setHighlight(card.dataset.listingId, false)));

  const gallery = card.querySelector("[data-gallery]");
  const images = [...card.querySelectorAll("[data-gallery-image]")];
  const dots = [...card.querySelectorAll("[data-gallery-dot]")];
  if (!gallery || images.length < 2) return;

  const showGalleryImage = (activeIndex) => {
    images.forEach((image, index) => {
      const isActive = index === activeIndex;
      image.classList.toggle("is-active", isActive);
      image.setAttribute("aria-hidden", String(!isActive));
    });
    dots.forEach((dot, index) => dot.classList.toggle("is-active", index === activeIndex));
    card.dataset.galleryIndex = String(activeIndex);
  };

  card.addEventListener("pointermove", (event) => {
    if (event.pointerType === "touch") return;
    const bounds = gallery.getBoundingClientRect();
    if (event.clientY < bounds.top || event.clientY > bounds.bottom) return;
    const position = Math.max(0, Math.min(bounds.width - 0.01, event.clientX - bounds.left));
    showGalleryImage(Math.floor(position / bounds.width * images.length));
  });
  card.addEventListener("pointerleave", () => showGalleryImage(0));
}

document.querySelectorAll(".listing-card").forEach(initializeListingCard);

markers.forEach((marker) => {
  ["mouseenter", "focus"].forEach((eventName) =>
    marker.addEventListener(eventName, () => setHighlight(marker.dataset.listingId, true)));
  ["mouseleave", "blur"].forEach((eventName) =>
    marker.addEventListener(eventName, () => setHighlight(marker.dataset.listingId, false)));
  marker.addEventListener("click", () => {
    const card = cards.find((item) => item.dataset.listingId === marker.dataset.listingId);
    catalogPage?.classList.remove("map-mode");
    card?.scrollIntoView({ behavior: "smooth", block: "center" });
    card?.focus({ preventScroll: true });
  });
});

const detailGallery = document.querySelector("[data-detail-gallery]");
const photoViewer = document.querySelector("[data-photo-viewer]");

if (detailGallery) {
  const detailImages = [...detailGallery.querySelectorAll("[data-detail-image]")];
  const detailDots = [...detailGallery.querySelectorAll("[data-detail-dot]")];
  const detailCounter = detailGallery.querySelector("[data-detail-counter]");
  const viewerImages = photoViewer ? [...photoViewer.querySelectorAll("[data-viewer-image]")] : [];
  const viewerDots = photoViewer ? [...photoViewer.querySelectorAll("[data-viewer-dot]")] : [];
  const viewerCounter = photoViewer?.querySelector("[data-viewer-counter]");
  let detailIndex = 0;
  let viewerIndex = 0;

  const normalizeGalleryIndex = (index) => (index + detailImages.length) % detailImages.length;
  const showDetailImage = (index) => {
    detailIndex = normalizeGalleryIndex(index);
    detailImages.forEach((image, imageIndex) => {
      const isActive = imageIndex === detailIndex;
      image.classList.toggle("is-active", isActive);
      image.setAttribute("aria-hidden", String(!isActive));
    });
    detailDots.forEach((dot, dotIndex) => {
      const isActive = dotIndex === detailIndex;
      dot.classList.toggle("is-active", isActive);
      dot.setAttribute("aria-current", String(isActive));
    });
    detailGallery.dataset.galleryIndex = String(detailIndex);
    if (detailCounter) detailCounter.textContent = `${detailIndex + 1} / ${detailImages.length}`;
  };
  const showViewerImage = (index) => {
    viewerIndex = normalizeGalleryIndex(index);
    viewerImages.forEach((image, imageIndex) => {
      const isActive = imageIndex === viewerIndex;
      image.classList.toggle("is-active", isActive);
      image.setAttribute("aria-hidden", String(!isActive));
    });
    viewerDots.forEach((dot, dotIndex) => {
      const isActive = dotIndex === viewerIndex;
      dot.classList.toggle("is-active", isActive);
      dot.setAttribute("aria-current", String(isActive));
    });
    if (photoViewer) photoViewer.dataset.galleryIndex = String(viewerIndex);
    if (viewerCounter) viewerCounter.textContent = `${viewerIndex + 1} / ${viewerImages.length}`;
  };

  detailGallery.querySelector("[data-detail-prev]")?.addEventListener("click", () => showDetailImage(detailIndex - 1));
  detailGallery.querySelector("[data-detail-next]")?.addEventListener("click", () => showDetailImage(detailIndex + 1));
  detailDots.forEach((dot) => dot.addEventListener("click", () => showDetailImage(Number(dot.dataset.galleryIndex))));
  detailGallery.querySelector("[data-detail-open]")?.addEventListener("click", () => {
    if (!photoViewer) return;
    showViewerImage(detailIndex);
    photoViewer.showModal();
  });

  photoViewer?.querySelector("[data-viewer-close]")?.addEventListener("click", () => photoViewer.close());
  photoViewer?.querySelector("[data-viewer-prev]")?.addEventListener("click", () => showViewerImage(viewerIndex - 1));
  photoViewer?.querySelector("[data-viewer-next]")?.addEventListener("click", () => showViewerImage(viewerIndex + 1));
  viewerDots.forEach((dot) => dot.addEventListener("click", () => showViewerImage(Number(dot.dataset.galleryIndex))));
  photoViewer?.addEventListener("click", (event) => { if (event.target === photoViewer) photoViewer.close(); });
  photoViewer?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowLeft") { event.preventDefault(); showViewerImage(viewerIndex - 1); }
    if (event.key === "ArrowRight") { event.preventDefault(); showViewerImage(viewerIndex + 1); }
  });
}

document.querySelector(".reset-filters")?.addEventListener("click", () => {
  document.querySelectorAll(".quick-filters > button").forEach((button) => button.classList.remove("active"));
});

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>'"]/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character]);
}

document.querySelectorAll("[data-location-editor]").forEach((editor) => {
  const input = editor.querySelector('[role="combobox"]');
  const list = editor.querySelector('[role="listbox"]');
  const latitude = editor.querySelector('[name="Input.Latitude"]');
  const longitude = editor.querySelector('[name="Input.Longitude"]');
  if (!input || !list || !latitude || !longitude) return;
  let timer;
  let request;
  let suggestions = [];
  let activeIndex = -1;
  const close = () => { list.hidden = true; list.replaceChildren(); input.setAttribute("aria-expanded", "false"); activeIndex = -1; };
  const select = (suggestion) => {
    input.value = suggestion.displayName;
    latitude.value = Number(suggestion.latitude).toFixed(6);
    longitude.value = Number(suggestion.longitude).toFixed(6);
    close();
  };
  const highlight = (index) => {
    const options = [...list.querySelectorAll('[role="option"]')];
    if (!options.length) return;
    activeIndex = (index + options.length) % options.length;
    options.forEach((option, optionIndex) => option.setAttribute("aria-selected", String(optionIndex === activeIndex)));
    options[activeIndex].scrollIntoView({ block: "nearest" });
  };
  const render = (items) => {
    suggestions = items.slice(0, 8);
    list.replaceChildren(...suggestions.map((suggestion, index) => {
      const button = document.createElement("button");
      button.type = "button"; button.role = "option"; button.textContent = suggestion.displayName; button.dataset.index = index;
      button.addEventListener("mousedown", (event) => { event.preventDefault(); select(suggestion); });
      return button;
    }));
    list.hidden = suggestions.length === 0;
    input.setAttribute("aria-expanded", String(suggestions.length > 0));
  };
  input.addEventListener("input", () => {
    latitude.value = ""; longitude.value = ""; clearTimeout(timer); request?.abort();
    const query = input.value.trim();
    if (query.length < 3) { close(); return; }
    timer = setTimeout(async () => {
      request = new AbortController();
      try {
        const response = await fetch(`${editor.dataset.suggestUrl}?q=${encodeURIComponent(query)}`, { signal: request.signal, headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error(String(response.status));
        render(await response.json());
      } catch (error) { if (error.name !== "AbortError") close(); }
    }, 350);
  });
  input.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") { event.preventDefault(); highlight(activeIndex + 1); }
    else if (event.key === "ArrowUp") { event.preventDefault(); highlight(activeIndex - 1); }
    else if (event.key === "Enter" && activeIndex >= 0) { event.preventDefault(); select(suggestions[activeIndex]); }
    else if (event.key === "Escape") close();
  });
  input.addEventListener("blur", () => setTimeout(close, 100));
});

document.querySelectorAll("[data-city-editor]").forEach((editor) => {
  const input = editor.querySelector('[role="combobox"]');
  const list = editor.querySelector('[role="listbox"]');
  const latitude = editor.querySelector('[name="Input.CityLatitude"]');
  const longitude = editor.querySelector('[name="Input.CityLongitude"]');
  if (!input || !list || !latitude || !longitude) return;
  let timer;
  let request;
  let suggestions = [];
  let activeIndex = -1;
  const close = () => { list.hidden = true; list.replaceChildren(); input.setAttribute("aria-expanded", "false"); activeIndex = -1; };
  const select = (suggestion) => {
    input.value = suggestion.displayName;
    latitude.value = Number(suggestion.latitude).toFixed(6);
    longitude.value = Number(suggestion.longitude).toFixed(6);
    close();
  };
  const highlight = (index) => {
    const options = [...list.querySelectorAll('[role="option"]')];
    if (!options.length) return;
    activeIndex = (index + options.length) % options.length;
    options.forEach((option, optionIndex) => option.setAttribute("aria-selected", String(optionIndex === activeIndex)));
    options[activeIndex].scrollIntoView({ block: "nearest" });
  };
  const render = (items) => {
    suggestions = items.slice(0, 8);
    list.replaceChildren(...suggestions.map((suggestion, index) => {
      const button = document.createElement("button");
      button.type = "button"; button.role = "option"; button.textContent = suggestion.displayName; button.dataset.index = index;
      button.addEventListener("mousedown", (event) => { event.preventDefault(); select(suggestion); });
      return button;
    }));
    list.hidden = suggestions.length === 0;
    input.setAttribute("aria-expanded", String(suggestions.length > 0));
  };
  input.addEventListener("input", () => {
    latitude.value = ""; longitude.value = ""; clearTimeout(timer); request?.abort();
    const query = input.value.trim();
    if (query.length < 3) { close(); return; }
    timer = setTimeout(async () => {
      request = new AbortController();
      try {
        const response = await fetch(`${editor.dataset.suggestUrl}?q=${encodeURIComponent(query)}`, { signal: request.signal, headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error(String(response.status));
        render(await response.json());
      } catch (error) { if (error.name !== "AbortError") close(); }
    }, 350);
  });
  input.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") { event.preventDefault(); highlight(activeIndex + 1); }
    else if (event.key === "ArrowUp") { event.preventDefault(); highlight(activeIndex - 1); }
    else if (event.key === "Enter" && activeIndex >= 0) { event.preventDefault(); select(suggestions[activeIndex]); }
    else if (event.key === "Escape") close();
  });
  input.addEventListener("blur", () => setTimeout(close, 100));
});

document.querySelectorAll("[data-search-address-editor]").forEach((editor) => {
  const input = editor.querySelector('[role="combobox"]');
  const list = editor.querySelector('[role="listbox"]');
  const latitude = editor.querySelector('[name="latitude"]');
  const longitude = editor.querySelector('[name="longitude"]');
  if (!input || !list || !latitude || !longitude) return;
  let timer;
  let request;
  let suggestions = [];
  let activeIndex = -1;
  const close = () => { list.hidden = true; list.replaceChildren(); input.setAttribute("aria-expanded", "false"); activeIndex = -1; };
  const select = (suggestion) => {
    input.value = suggestion.displayName;
    latitude.value = Number(suggestion.latitude).toFixed(6);
    longitude.value = Number(suggestion.longitude).toFixed(6);
    input.setCustomValidity("");
    close();
  };
  const highlight = (index) => {
    const options = [...list.querySelectorAll('[role="option"]')];
    if (!options.length) return;
    activeIndex = (index + options.length) % options.length;
    options.forEach((option, optionIndex) => option.setAttribute("aria-selected", String(optionIndex === activeIndex)));
    options[activeIndex].scrollIntoView({ block: "nearest" });
  };
  const render = (items) => {
    suggestions = items.slice(0, 8);
    list.replaceChildren(...suggestions.map((suggestion, index) => {
      const button = document.createElement("button");
      button.type = "button"; button.role = "option"; button.textContent = suggestion.displayName; button.dataset.index = index;
      button.addEventListener("mousedown", (event) => { event.preventDefault(); select(suggestion); });
      return button;
    }));
    list.hidden = suggestions.length === 0;
    input.setAttribute("aria-expanded", String(suggestions.length > 0));
  };
  input.addEventListener("input", () => {
    latitude.value = ""; longitude.value = ""; input.setCustomValidity(""); clearTimeout(timer); request?.abort();
    const query = input.value.trim();
    if (query.length < 3) { close(); return; }
    timer = setTimeout(async () => {
      request = new AbortController();
      try {
        const response = await fetch(`${editor.dataset.suggestUrl}?q=${encodeURIComponent(query)}`, { signal: request.signal, headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error(String(response.status));
        render(await response.json());
      } catch (error) { if (error.name !== "AbortError") close(); }
    }, 350);
  });
  input.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") { event.preventDefault(); highlight(activeIndex + 1); }
    else if (event.key === "ArrowUp") { event.preventDefault(); highlight(activeIndex - 1); }
    else if (event.key === "Enter" && activeIndex >= 0) { event.preventDefault(); select(suggestions[activeIndex]); }
    else if (event.key === "Escape") close();
  });
  input.addEventListener("blur", () => setTimeout(close, 100));
  editor.addEventListener("submit", (event) => {
    if (latitude.value && longitude.value) return;
    event.preventDefault();
    input.setCustomValidity("Выберите адрес из списка подсказок.");
    input.reportValidity();
  });
});

document.querySelectorAll("[data-avatar-editor]").forEach((editor) => {
  const file = editor.querySelector("[data-avatar-file]");
  const dialog = editor.querySelector("[data-avatar-dialog]");
  const stage = editor.querySelector("[data-avatar-stage]");
  const image = editor.querySelector("[data-avatar-image]");
  const zoom = editor.querySelector("[data-avatar-zoom]");
  const output = editor.querySelector("[data-avatar-data]");
  const preview = editor.querySelector("[data-avatar-preview]");
  if (!file || !dialog || !stage || !image || !zoom || !output || !preview) return;
  let objectUrl;
  let offsetX = 0, offsetY = 0, dragging = false, pointerX = 0, pointerY = 0;
  const stageSize = () => stage.getBoundingClientRect().width;
  const baseScale = () => Math.max(stageSize() / image.naturalWidth, stageSize() / image.naturalHeight);
  const render = () => {
    const scale = baseScale() * Number(zoom.value);
    image.style.width = `${image.naturalWidth * scale}px`; image.style.height = `${image.naturalHeight * scale}px`;
    image.style.transform = `translate(calc(-50% + ${offsetX}px), calc(-50% + ${offsetY}px))`;
  };
  file.addEventListener("change", () => {
    const selected = file.files?.[0];
    if (!selected || selected.size > 10 * 1024 * 1024 || !selected.type.startsWith("image/")) return;
    if (objectUrl) URL.revokeObjectURL(objectUrl); objectUrl = URL.createObjectURL(selected); image.src = objectUrl;
    image.onload = () => { offsetX = 0; offsetY = 0; zoom.value = "1"; render(); dialog.showModal(); };
  });
  zoom.addEventListener("input", render);
  stage.addEventListener("pointerdown", (event) => { dragging = true; pointerX = event.clientX; pointerY = event.clientY; stage.setPointerCapture(event.pointerId); });
  stage.addEventListener("pointermove", (event) => { if (!dragging) return; offsetX += event.clientX - pointerX; offsetY += event.clientY - pointerY; pointerX = event.clientX; pointerY = event.clientY; render(); });
  stage.addEventListener("pointerup", () => { dragging = false; });
  editor.querySelector("[data-avatar-cancel]")?.addEventListener("click", () => dialog.close());
  editor.querySelector("[data-avatar-accept]")?.addEventListener("click", () => {
    const canvas = document.createElement("canvas"); canvas.width = 512; canvas.height = 512;
    const ratio = 512 / stageSize(); const scale = baseScale() * Number(zoom.value) * ratio;
    const width = image.naturalWidth * scale, height = image.naturalHeight * scale;
    const context = canvas.getContext("2d"); context.fillStyle = "#e8e3dc"; context.fillRect(0, 0, 512, 512);
    context.drawImage(image, (512 - width) / 2 + offsetX * ratio, (512 - height) / 2 + offsetY * ratio, width, height);
    output.value = canvas.toDataURL("image/jpeg", .86);
    preview.innerHTML = ""; const result = document.createElement("img"); result.src = output.value; result.alt = "Новый аватар"; preview.append(result); dialog.close();
  });
});

document.querySelectorAll('input[placeholder*="+7 (999)"]').forEach((input) => {
  input.addEventListener("input", () => {
    if (input.value.includes("@") || /[A-Za-zА-Яа-я]/.test(input.value)) return;
    let digits = input.value.replace(/\D/g, "");
    if (digits.startsWith("8")) digits = `7${digits.slice(1)}`;
    if (!digits.startsWith("7")) digits = `7${digits}`;
    digits = digits.slice(0, 11);
    let value = "+7";
    if (digits.length > 1) value += ` (${digits.slice(1, 4)}`;
    if (digits.length >= 4) value += ")";
    if (digits.length > 4) value += ` ${digits.slice(4, 7)}`;
    if (digits.length > 7) value += `-${digits.slice(7, 9)}`;
    if (digits.length > 9) value += `-${digits.slice(9, 11)}`;
    input.value = value;
  });
});

const feedLoader = document.querySelector("[data-feed-loader]");
if (feedLoader && !feedLoader.hidden) {
  const listingGrid = feedLoader.closest(".listing-grid");
  const feedStatus = feedLoader.querySelector("[data-feed-status]");
  const retryButton = feedLoader.querySelector("[data-feed-retry]");
  const requestToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  let feedLoading = false;

  const createFeedCard = (item) => {
    const article = document.createElement("article");
    article.className = "listing-card";
    article.dataset.listingId = item.id;
    article.dataset.galleryCount = String(item.images.length);
    const images = item.images.map((url, index) => `<img class="listing-card__image ${index === 0 ? "is-active" : ""}" data-gallery-image data-gallery-index="${index}" src="${escapeHtml(url)}" alt="${escapeHtml(index === 0 ? item.title : `${item.title}, фото ${index + 1}`)}" width="600" height="450" loading="lazy" aria-hidden="${index === 0 ? "false" : "true"}" />`).join("");
    const dots = item.images.length > 1 ? `<span class="listing-card__dots" aria-hidden="true">${item.images.map((_, index) => `<i class="listing-card__dot ${index === 0 ? "is-active" : ""}" data-gallery-dot data-gallery-index="${index}"></i>`).join("")}</span>` : "";
    let favorite = "";
    if (item.favoriteUrl?.includes("handler=ToggleFavorite") && requestToken) {
      favorite = `<form method="post" action="${escapeHtml(item.favoriteUrl)}" class="favorite-form"><input type="hidden" name="__RequestVerificationToken" value="${escapeHtml(requestToken)}" /><input type="hidden" name="id" value="${escapeHtml(item.id)}" /><input type="hidden" name="returnUrl" value="${escapeHtml(item.returnUrl)}" /><button class="favorite-button" type="submit" aria-label="${item.isFavorite ? "Убрать из избранного" : "Добавить в избранное"}" aria-pressed="${item.isFavorite}"><svg><use href="#i-heart"></use></svg></button></form>`;
    } else if (item.favoriteUrl) {
      favorite = `<a class="favorite-button" href="${escapeHtml(item.favoriteUrl)}" aria-label="Войти и добавить в избранное"><svg><use href="#i-heart"></use></svg></a>`;
    }
    article.innerHTML = `<div class="listing-card__media" data-gallery aria-label="${item.images.length} фото. Проведите мышью по изображению, чтобы пролистать.">${images}${dots}${favorite}${item.verifiedIdentity ? '<span class="verified-badge"><svg><use href="#i-check"></use></svg> Личность подтверждена</span>' : ""}</div><div class="listing-card__body"><strong class="listing-price">${escapeHtml(item.priceLabel)}</strong><h2><a class="listing-card__link" href="${escapeHtml(item.detailUrl)}">${escapeHtml(item.title)}</a></h2><p class="listing-description">${escapeHtml(item.description)}</p><div class="listing-meta"><span><svg><use href="#i-pin"></use></svg>${escapeHtml(item.location)}</span><span>${escapeHtml(item.publishedLabel)}</span></div>${item.negotiable ? '<span class="negotiable-badge">Торг</span>' : ""}</div>`;
    initializeListingCard(article);
    return article;
  };

  const observer = new IntersectionObserver(async (entries) => {
    if (!entries.some((entry) => entry.isIntersecting) || feedLoading) return;
    feedLoading = true;
    retryButton.hidden = true;
    feedLoader.classList.remove("has-error", "is-complete");
    if (feedStatus) feedStatus.textContent = "Загружаем ещё объявления…";
    try {
      const url = new URL(window.location.href);
      url.searchParams.set("handler", "More");
      url.searchParams.set("offset", feedLoader.dataset.offset || "0");
      const response = await fetch(url, { headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error(`Feed ${response.status}`);
      const result = await response.json();
      result.items.forEach((item) => listingGrid.insertBefore(createFeedCard(item), feedLoader));
      const nextOffset = Number(feedLoader.dataset.offset || 0) + result.items.length;
      feedLoader.dataset.offset = String(nextOffset);
      feedLoader.dataset.total = String(result.total);
      if (!result.hasMore || result.items.length === 0) {
        observer.disconnect();
        feedLoader.classList.add("is-complete");
        if (feedStatus) feedStatus.textContent = `Все ${result.total} объявлений загружены`;
      } else if (feedStatus) {
        feedStatus.textContent = `Показано ${nextOffset} из ${result.total}. Прокрутите ниже, чтобы загрузить ещё`;
      }
    } catch {
      observer.unobserve(feedLoader);
      feedLoader.classList.add("has-error");
      if (feedStatus) feedStatus.textContent = "Не удалось загрузить следующую часть";
      retryButton.hidden = false;
    } finally {
      feedLoading = false;
    }
  }, { rootMargin: "600px 0px" });
  observer.observe(feedLoader);
  retryButton.addEventListener("click", () => observer.observe(feedLoader));
}

const pagedListSelectors = [
  ".saved-search-list", ".recommendations-page .recommendation-grid", ".search-history-list",
  ".session-list", ".dialog-list", ".my-listings", ".favorite-grid",
  ".seller-listings", ".seller-reviews", ".moderation-queue", ".refund-list",
  ".payment-list", ".notification-list", ".flag-list", ".history-list", ".audit-list", ".finding-list", ".admin-category-list",
  ".admin-table-wrap tbody"
];
pagedListSelectors.forEach((selector) => document.querySelectorAll(selector).forEach((list) => {
  if (list.closest(".listing-grid")) return;
  list.dataset.pagedList = "";
  list.dataset.pageSize ||= "10";
  [...list.children].forEach((item) => item.dataset.pageItem = "");
}));

function initializePagedList(list) {
  if (list.dataset.pagerReady === "true") return;
  list.dataset.pagerReady = "true";
  const items = [...list.querySelectorAll(":scope > [data-page-item]")];
  const pageSize = Math.max(1, Number(list.dataset.pageSize || 10));
  const pageCount = Math.ceil(items.length / pageSize);
  if (pageCount <= 1) return;
  let activePage = 1;
  const host = list.closest("[data-pager-host], .admin-table-wrap") || list;
  const navigation = document.createElement("nav");
  navigation.className = "list-pagination";
  navigation.setAttribute("aria-label", list.dataset.pagerLabel || "Страницы списка");

  const render = (scroll = false) => {
    items.forEach((item, index) => { item.hidden = Math.floor(index / pageSize) + 1 !== activePage; });
    navigation.replaceChildren();
    const makeButton = (label, page, current = false) => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = current ? "active" : "";
      button.textContent = label;
      button.disabled = page < 1 || page > pageCount;
      if (current) button.setAttribute("aria-current", "page");
      button.addEventListener("click", () => { activePage = page; render(true); });
      return button;
    };
    navigation.append(makeButton("← Назад", activePage - 1));
    const visiblePages = [...new Set([1, pageCount, activePage - 1, activePage, activePage + 1].filter((page) => page >= 1 && page <= pageCount))];
    visiblePages.forEach((page, index) => {
      if (index > 0 && page - visiblePages[index - 1] > 1) { const gap = document.createElement("span"); gap.textContent = "…"; navigation.append(gap); }
      navigation.append(makeButton(String(page), page, page === activePage));
    });
    navigation.append(makeButton("Вперёд →", activePage + 1));
    const status = document.createElement("span");
    status.className = "list-pagination__status";
    status.setAttribute("aria-live", "polite");
    status.textContent = `${(activePage - 1) * pageSize + 1}–${Math.min(activePage * pageSize, items.length)} из ${items.length}`;
    navigation.append(status);
    if (scroll) host.scrollIntoView({ behavior: "smooth", block: "start" });
  };
  host.insertAdjacentElement("afterend", navigation);
  render();
}

window.marketplacePagination = { initialize: initializePagedList };
document.querySelectorAll("[data-paged-list]").forEach(initializePagedList);

document.querySelectorAll(".view-toggle button").forEach((button) => {
  button.addEventListener("click", () => {
    document.querySelectorAll(".view-toggle button").forEach((item) => {
      const active = item === button;
      item.classList.toggle("active", active);
      item.setAttribute("aria-pressed", String(active));
    });
  });
});

document.querySelector(".mobile-map-button")?.addEventListener("click", (event) => {
  const mapMode = catalogPage?.classList.toggle("map-mode") ?? false;
  event.currentTarget.innerHTML = mapMode
    ? '<svg><use href="#i-list"></use></svg> Список'
    : '<svg><use href="#i-pin"></use></svg> Карта';
});

function showMarketplaceList() {
  catalogPage?.classList.remove("map-mode");
  const layout = document.querySelector(".catalog-layout");
  layout?.classList.remove("mode-map", "mode-split");
  layout?.classList.add("mode-list");
  const mobileButton = document.querySelector(".mobile-map-button");
  if (mobileButton) mobileButton.innerHTML = '<svg><use href="#i-pin"></use></svg> Карта';
  const url = new URL(location.href);
  url.searchParams.set("mode", "list");
  history.replaceState(null, "", url);
}

window.marketplaceShowList = showMarketplaceList;
document.querySelector(".show-list-button")?.addEventListener("click", showMarketplaceList);
document.querySelector("[data-map-retry]")?.addEventListener("click", () => location.reload());

document.querySelectorAll("[data-dialog-open]").forEach((button) => {
  button.addEventListener("click", () => document.getElementById(button.dataset.dialogOpen)?.showModal());
});

document.querySelectorAll("[data-dialog-close]").forEach((button) => {
  button.addEventListener("click", () => button.closest("dialog")?.close());
});

document.querySelectorAll("dialog[data-close-on-backdrop]").forEach((dialog) => {
  dialog.addEventListener("click", (event) => {
    if (event.target === dialog) dialog.close();
  });
});

document.querySelector("[data-category-more]")?.addEventListener("click", (event) => {
  const button = event.currentTarget;
  const strip = button.closest("[data-category-strip]");
  if (!strip) return;
  const expanded = strip.dataset.expanded !== "true";
  strip.dataset.expanded = String(expanded);
  button.setAttribute("aria-expanded", String(expanded));
  const label = button.querySelector("span");
  if (label) label.textContent = expanded ? "Свернуть" : "Ещё";
  if (expanded) strip.querySelector(".category-chip--extra")?.focus({ preventScroll: true });
});

document.querySelector("[data-sort-select]")?.addEventListener("change", (event) => {
  event.currentTarget.closest("[data-sort-form]")?.requestSubmit();
});

document.querySelectorAll("form[data-clean-query]").forEach((form) => {
  form.addEventListener("submit", () => {
    form.querySelectorAll("input[name], select[name]").forEach((control) => {
      if (control.value.trim() === "") control.disabled = true;
    });
  });
});

document.querySelector("[data-geolocate]")?.addEventListener("click", (event) => {
  const status = document.querySelector(".geo-permission__status");
  if (!navigator.geolocation) {
    if (status) status.textContent = "Геолокация не поддерживается. Выберите город вручную.";
    return;
  }
  event.currentTarget.disabled = true;
  if (status) status.textContent = "Запрашиваем разрешение…";
  navigator.geolocation.getCurrentPosition(async ({ coords }) => {
    try {
      const response = await fetch(`/?handler=ResolveLocation&latitude=${encodeURIComponent(coords.latitude)}&longitude=${encodeURIComponent(coords.longitude)}`);
      const result = await response.json();
      const target = new URL(window.location.href);
      target.searchParams.delete("handler");
      target.searchParams.delete("latitude");
      target.searchParams.delete("longitude");
      target.searchParams.set("city", result.city);
      target.searchParams.set("address", result.address ?? result.city);
      target.searchParams.set("latitude", result.latitude ?? coords.latitude);
      target.searchParams.set("longitude", result.longitude ?? coords.longitude);
      window.location.assign(target);
    } catch {
      if (status) status.textContent = "Не удалось определить адрес. Заполните его вручную.";
      event.currentTarget.disabled = false;
    }
  }, () => {
    if (status) status.textContent = "Доступ не предоставлен. Заполните адрес вручную — поиск продолжает работать.";
    event.currentTarget.disabled = false;
  }, { enableHighAccuracy: false, timeout: 8000, maximumAge: 300000 });
});
