const statusEl = document.getElementById("status");

document.getElementById("capture").addEventListener("click", async () => {
  statusEl.textContent = "Đang đọc trang sản phẩm...";
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const [{ result }] = await chrome.scripting.executeScript({
    target: { tabId: tab.id },
    func: collectProductSource
  });

  if (!result?.selectedImage && !result?.ogImage) {
    statusEl.textContent = "Không tìm thấy ảnh sản phẩm trên trang này.";
    return;
  }

  statusEl.textContent = "Đang gửi về backend Hoàn Doãn...";
  const imageUrl = result.selectedImage || result.ogImage;
  const imageResponse = await fetch(imageUrl, { credentials: "include" });
  const imageBlob = await imageResponse.blob();
  const form = new FormData();
  form.append("metadata", JSON.stringify(result));
  form.append("image", imageBlob, "captured-product-image");

  const response = await fetch("http://127.0.0.1:5000/api/admin/trusted-product-index/capture", {
    method: "POST",
    body: form
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    statusEl.textContent = data.error || "Lưu nguồn thất bại.";
    return;
  }

  statusEl.textContent = `Đã lưu nguồn.\n${data.productName || result.ogTitle || result.documentTitle}\n${data.canonicalUrl || result.canonicalUrl}`;
});

function collectProductSource() {
  const canonicalUrl = document.querySelector('link[rel="canonical"]')?.href || location.href;
  const ogTitle = document.querySelector('meta[property="og:title"]')?.content || "";
  const ogImage = document.querySelector('meta[property="og:image"]')?.content || "";
  const productJsonNodes = [...document.querySelectorAll('script[type="application/ld+json"]')]
    .map((node) => node.textContent || "")
    .filter((text) => /"@type"\s*:\s*"Product"|Product/i.test(text));
  const productDataJson = productJsonNodes[0] || "";
  const selectedImage = findLikelyProductImage(ogImage);
  const productData = parseProductJson(productDataJson);

  return {
    sourceUrl: location.href,
    canonicalUrl,
    documentTitle: document.title,
    ogTitle,
    ogImage,
    selectedImage,
    productDataJson,
    sourceDomain: location.hostname.replace(/^www\./, ""),
    brand: productData.brand || "",
    productName: productData.name || ""
  };
}

function findLikelyProductImage(fallback) {
  const images = [...document.images]
    .map((img) => ({
      src: img.currentSrc || img.src,
      area: (img.naturalWidth || img.width || 0) * (img.naturalHeight || img.height || 0),
      alt: img.alt || ""
    }))
    .filter((item) => item.src && item.area > 12000 && !/logo|icon|sprite/i.test(item.src + " " + item.alt))
    .sort((a, b) => b.area - a.area);
  return images[0]?.src || fallback || "";
}

function parseProductJson(text) {
  try {
    const root = JSON.parse(text);
    const nodes = Array.isArray(root) ? root : [root, ...(root["@graph"] || [])];
    const product = nodes.find((node) => JSON.stringify(node["@type"] || "").includes("Product")) || {};
    const brand = typeof product.brand === "object" ? product.brand.name : product.brand;
    return { name: product.name || "", brand: brand || "" };
  } catch {
    return {};
  }
}
