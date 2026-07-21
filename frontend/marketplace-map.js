import maplibregl from "maplibre-gl";
import "maplibre-gl/dist/maplibre-gl.css";
import { Protocol } from "pmtiles";
import { namedFlavor } from "@protomaps/basemaps";

const container = document.querySelector("[data-marketplace-map]");
if (container) initializeMap(container);

async function initializeMap(shell) {
  const canvas = shell.querySelector("[data-map-canvas]");
  const state = shell.querySelector("[data-map-state]");
  const stateText = shell.querySelector("[data-map-state-text]");
  const retry = shell.querySelector("[data-map-retry]");
  const searchWhileMoving = shell.querySelector("[data-map-search-moving]");
  let map;
  let request;
  let markers = [];
  let protocolAdded = false;
  let resizeFrame;
  let renderedWidth = 0;
  let renderedHeight = 0;

  const resizeMap = () => {
    if (!map) return;
    if (resizeFrame) cancelAnimationFrame(resizeFrame);
    resizeFrame = requestAnimationFrame(() => {
      resizeFrame = requestAnimationFrame(() => {
        resizeFrame = undefined;
        const bounds = canvas.getBoundingClientRect();
        const width = Math.round(bounds.width);
        const height = Math.round(bounds.height);
        if (width <= 0 || height <= 0 || (width === renderedWidth && height === renderedHeight)) return;
        renderedWidth = width;
        renderedHeight = height;
        map.resize();
      });
    });
  };

  const setState = (message, error = false) => {
    if (!state) return;
    if (stateText) stateText.textContent = message;
    else state.textContent = message;
    state.hidden = !message;
    state.classList.toggle("is-error", error);
    if (retry) retry.hidden = !error;
  };
  const showList = () => {
    if (typeof window.marketplaceShowList === "function") window.marketplaceShowList();
    else document.querySelector(".catalog-page")?.classList.remove("map-mode");
  };
  const clearMarkers = () => { markers.forEach(marker => marker.remove()); markers = []; };
  const styleFor = config => {
    const theme = document.documentElement.dataset.theme === "dark" ? "dark" : "light";
    const flavor = namedFlavor(theme);
    const source = { source: "basemap" };
    const mapLayers = [
      { id: "background", type: "background", paint: { "background-color": flavor.earth } },
      { id: "boundaries", type: "line", ...source, "source-layer": "boundaries", paint: { "line-color": flavor.boundaries, "line-width": ["interpolate", ["linear"], ["zoom"], 4, .5, 13, 1.2] } },
      { id: "roads-casing", type: "line", ...source, "source-layer": "roads", filter: ["!", ["==", ["get", "kind"], "rail"]], paint: { "line-color": flavor.minor_casing, "line-width": ["interpolate", ["linear"], ["zoom"], 5, .7, 11, 2.6, 16, 7] } },
      { id: "roads", type: "line", ...source, "source-layer": "roads", filter: ["!", ["==", ["get", "kind"], "rail"]], paint: { "line-color": flavor.minor_b, "line-width": ["interpolate", ["linear"], ["zoom"], 5, .35, 11, 1.5, 16, 5] } },
      { id: "railways", type: "line", ...source, "source-layer": "roads", filter: ["==", ["get", "kind"], "rail"], paint: { "line-color": flavor.railway, "line-width": ["interpolate", ["linear"], ["zoom"], 8, .5, 16, 1.4], "line-dasharray": [2, 2] } },
      { id: "place-labels", type: "symbol", ...source, "source-layer": "places", layout: { "text-field": ["coalesce", ["get", "name:ru"], ["get", "name"]], "text-font": ["Noto Sans Medium"], "text-size": ["interpolate", ["linear"], ["zoom"], 5, 11, 13, 14] }, paint: { "text-color": flavor.city_label, "text-halo-color": flavor.city_label_halo, "text-halo-width": 1.5 } },
      { id: "house-numbers", type: "symbol", ...source, "source-layer": "address", filter: ["!=", ["get", "addr_housenumber"], null], minzoom: 14, layout: { "text-field": ["get", "addr_housenumber"], "text-font": ["Noto Sans Regular"], "text-size": 10 }, paint: { "text-color": flavor.address_label, "text-halo-color": flavor.address_label_halo, "text-halo-width": 1 } }
    ];
    const assetsBaseUrl = new URL(config.assetsBaseUrl, location.origin).href.replace(/\/$/, "");
    const basemapUrl = config.basemapUrl.startsWith("/")
      ? config.basemapUrl
      : new URL(config.basemapUrl, location.origin).href;
    return {
      version: 8,
      glyphs: `${assetsBaseUrl}/assets/fonts/{fontstack}/{range}.pbf`,
      sprite: `${assetsBaseUrl}/assets/sprites/v4/${theme}`,
      sources: { basemap: { type: "vector", url: `pmtiles://${basemapUrl}`, attribution: config.attributionHtml } },
      layers: mapLayers
    };
  };
  const filters = () => {
    const source = new URLSearchParams(location.search);
    const target = new URLSearchParams();
    const mapping = { q: "query", category: "category", minPrice: "priceFrom", maxPrice: "priceTo", condition: "condition", dealType: "dealType", hasPhoto: "hasPhoto" };
    Object.entries(mapping).forEach(([from, to]) => { if (source.get(from)) target.set(to, source.get(from)); });
    return target;
  };
  const connectCard = (element, listingId) => {
    const findCard = () => document.querySelector(`.listing-card[data-listing-id="${CSS.escape(listingId)}"]`);
    ["mouseenter", "focus"].forEach(name => element.addEventListener(name, () => findCard()?.setAttribute("data-map-highlighted", "true")));
    ["mouseleave", "blur"].forEach(name => element.addEventListener(name, () => findCard()?.removeAttribute("data-map-highlighted")));
    element.addEventListener("click", () => {
      const card = findCard();
      showList();
      card?.scrollIntoView({ behavior: "smooth", block: "center" });
      card?.focus({ preventScroll: true });
    });
  };
  const render = data => {
    clearMarkers();
    data.clusters.forEach(cluster => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "map-cluster-marker";
      button.textContent = cluster.count;
      button.setAttribute("aria-label", `Показать ${cluster.count} объявлений`);
      button.addEventListener("click", () => {
        const b = cluster.bounds;
        const equal = b.west === b.east && b.south === b.north;
        if (equal) map.flyTo({ center: [cluster.longitude, cluster.latitude], zoom: Math.min(map.getZoom() + 3, 18) });
        else map.fitBounds([[b.west, b.south], [b.east, b.north]], { padding: 90, maxZoom: 17 });
      });
      markers.push(new maplibregl.Marker({ element: button }).setLngLat([cluster.longitude, cluster.latitude]).addTo(map));
    });
    data.items.forEach(item => {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "map-item-marker";
      button.dataset.listingId = item.listingId;
      button.title = `${item.title}, ${item.priceLabel}`;
      button.setAttribute("aria-label", `${item.title}, ${item.priceLabel}`);
      const image = document.createElement("img");
      image.src = item.previewImageUrl;
      image.alt = "";
      image.addEventListener("error", () => button.classList.add("image-missing"));
      const label = document.createElement("span");
      label.className = "map-item-marker__label";
      label.textContent = item.priceLabel;
      button.append(image, label);
      connectCard(button, String(item.listingId));
      const card = document.querySelector(`.listing-card[data-listing-id="${CSS.escape(String(item.listingId))}"]`);
      if (card && card.dataset.mapBindingReady !== "true") {
        card.dataset.mapBindingReady = "true";
        const findMarker = () => document.querySelector(`.map-item-marker[data-listing-id="${CSS.escape(String(item.listingId))}"]`);
        ["mouseenter", "focusin"].forEach(name => card.addEventListener(name, () => findMarker()?.setAttribute("data-card-highlighted", "true")));
        ["mouseleave", "focusout"].forEach(name => card.addEventListener(name, () => findMarker()?.removeAttribute("data-card-highlighted")));
      }
      markers.push(new maplibregl.Marker({ element: button }).setLngLat([item.longitude, item.latitude]).addTo(map));
    });
    setState(data.total ? "" : "В этой области объявлений нет");
  };
  const refresh = async () => {
    if (!map) return;
    request?.abort();
    request = new AbortController();
    const bounds = map.getBounds();
    const params = filters();
    Object.entries({ west: bounds.getWest(), south: bounds.getSouth(), east: bounds.getEast(), north: bounds.getNorth(), zoom: map.getZoom() }).forEach(([key, value]) => params.set(key, value));
    try {
      const response = await fetch(`/maps-api/api/v1/map/clusters?${params}`, { signal: request.signal, headers: { Accept: "application/json" } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      render(await response.json());
    } catch (error) {
      if (error.name !== "AbortError") setState("Не удалось обновить объявления на карте. Список продолжает работать.", true);
    }
  };

  try {
    setState("Загружаем карту…");
    const response = await fetch(shell.dataset.configUrl || "/maps-api/api/v1/map/config", { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const config = await response.json();
    if (!config.enabled) throw new Error(config.reason || "Карта отключена");
    if (!protocolAdded) { const protocol = new Protocol(); maplibregl.addProtocol("pmtiles", protocol.tile); protocolAdded = true; }
    const urlParams = new URLSearchParams(location.search);
    const urlZoom = Number(urlParams.get("zoom"));
    const urlLongitude = Number(urlParams.get("longitude"));
    const urlLatitude = Number(urlParams.get("latitude"));
    const hasUrlCenter = urlParams.has("longitude") && urlParams.has("latitude") &&
      Number.isFinite(urlLongitude) && Number.isFinite(urlLatitude) &&
      urlLongitude >= -180 && urlLongitude <= 180 && urlLatitude >= -90 && urlLatitude <= 90;
    const initialCenter = hasUrlCenter
      ? [urlLongitude, urlLatitude]
      : [config.defaultView.longitude, config.defaultView.latitude];
    map = new maplibregl.Map({
      container: canvas,
      style: styleFor(config),
      center: initialCenter,
      zoom: Number.isFinite(urlZoom) && urlZoom >= 2 ? urlZoom : config.defaultView.zoom,
      minZoom: config.zoom.min,
      maxZoom: config.zoom.max,
      renderWorldCopies: false,
      attributionControl: true
    });
    const initialBounds = canvas.getBoundingClientRect();
    renderedWidth = Math.round(initialBounds.width);
    renderedHeight = Math.round(initialBounds.height);
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), "bottom-right");
    const resizeObserver = new ResizeObserver(resizeMap);
    resizeObserver.observe(shell);
    window.addEventListener("resize", resizeMap, { passive: true });
    window.addEventListener("orientationchange", resizeMap, { passive: true });
    window.visualViewport?.addEventListener("resize", resizeMap, { passive: true });
    shell.addEventListener("transitionend", resizeMap);
    const storedSearchPreference = localStorage.getItem("marketplace-map-search-moving");
    if (storedSearchPreference === "false") searchWhileMoving?.setAttribute("aria-pressed", "false");
    map.once("load", () => { resizeMap(); shell.classList.add("map-ready"); setState(""); refresh(); });
    map.on("moveend", () => { if (searchWhileMoving?.getAttribute("aria-pressed") !== "false") refresh(); });
    searchWhileMoving?.addEventListener("click", () => {
      const active = searchWhileMoving.getAttribute("aria-pressed") !== "true";
      searchWhileMoving.setAttribute("aria-pressed", String(active));
      localStorage.setItem("marketplace-map-search-moving", String(active));
      if (active) refresh();
    });
    new MutationObserver(() => {
      const center = map.getCenter(), zoom = map.getZoom();
      map.setStyle(styleFor(config));
      map.once("style.load", () => { map.jumpTo({ center, zoom }); refresh(); });
    }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
  } catch (error) {
    setState(`Карта временно недоступна: ${error.message}. Объявления доступны списком.`, true);
    shell.classList.add("map-unavailable");
  }
}
