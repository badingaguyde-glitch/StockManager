// site.js
// Custom JavaScript for StockManager

console.log("StockManager site.js initialized.");

document.addEventListener('DOMContentLoaded', function () {
    // -------------------------------------------------------------
    // 1. Ülke Bayraklı Telefon Doğrulama Entegrasyonu (intl-tel-input)
    // -------------------------------------------------------------
    function validatePhoneInput(input, iti) {
        const errorMsg = input.closest(".mb-3")?.querySelector(".phone-validation-error");
        if (!errorMsg) return;
        
        if (input.value.trim()) {
            if (iti.isValidNumber()) {
                input.classList.remove("is-invalid");
                input.classList.add("is-valid");
                errorMsg.textContent = "";
                errorMsg.style.display = "none";
            } else {
                input.classList.remove("is-valid");
                input.classList.add("is-invalid");
                errorMsg.textContent = "Geçersiz telefon numarası formatı.";
                errorMsg.style.display = "block";
            }
        } else {
            input.classList.remove("is-valid", "is-invalid");
            errorMsg.textContent = "";
            errorMsg.style.display = "none";
        }
    }

    document.querySelectorAll(".intl-phone-input").forEach(function (input) {
        const iti = window.intlTelInput(input, {
            initialCountry: "tr",
            preferredCountries: ["tr", "az", "de", "us"],
            utilsScript: "https://cdn.jsdelivr.net/npm/intl-tel-input@18.2.1/build/js/utils.js"
        });
        
        // Store instance
        input.itiInstance = iti;

        // Validation events
        input.addEventListener("blur", function () {
            validatePhoneInput(input, iti);
        });
        input.addEventListener("input", function () {
            validatePhoneInput(input, iti);
        });
    });

    // Form submit validation & formatting
    document.querySelectorAll("form").forEach(function (form) {
        form.addEventListener("submit", function (e) {
            let valid = true;
            form.querySelectorAll(".intl-phone-input").forEach(function (input) {
                const iti = input.itiInstance;
                if (iti) {
                    validatePhoneInput(input, iti);
                    if (input.value.trim() && !iti.isValidNumber()) {
                        valid = false;
                    } else if (iti.isValidNumber()) {
                        // Set the value to the full E.164 number (+905XXXXXXXXX)
                        input.value = iti.getNumber();
                    }
                }
            });
            if (!valid) {
                e.preventDefault();
                e.stopPropagation();
            }
        });
    });

    // -------------------------------------------------------------
    // 2. Spotlight (Ctrl + K) Arama Menüsü & Kısayollar
    // -------------------------------------------------------------
    let lastKey = "";
    let lastKeyTime = 0;

    document.addEventListener("keydown", function (e) {
        // Spotlight Modal Tetikleyici (Ctrl + K)
        if (e.ctrlKey && e.key.toLowerCase() === 'k') {
            e.preventDefault();
            const searchModalEl = document.getElementById("globalQuickMenuModal");
            if (searchModalEl) {
                const modal = bootstrap.Modal.getOrCreateInstance(searchModalEl);
                modal.show();
            }
        }

        // Hızlı Yönlendirme Kısayolları (G + P, G + C, G + R)
        const activeEl = document.activeElement;
        const isInput = activeEl.tagName === "INPUT" || activeEl.tagName === "TEXTAREA" || activeEl.isContentEditable || activeEl.tagName === "SELECT";
        
        if (!isInput) {
            const now = Date.now();
            const key = e.key.toLowerCase();
            
            if (lastKey === 'g' && (now - lastKeyTime < 1500)) {
                if (key === 'p') {
                    e.preventDefault();
                    window.location.href = "/Products";
                } else if (key === 'c') {
                    e.preventDefault();
                    window.location.href = "/Customers";
                } else if (key === 'r') {
                    e.preventDefault();
                    window.location.href = "/Reports";
                }
            }
            
            lastKey = key;
            lastKeyTime = now;
        }
    });

    // Modal açıldığında odaklanma ve temizleme
    const searchModalEl = document.getElementById("globalQuickMenuModal");
    if (searchModalEl) {
        searchModalEl.addEventListener("shown.bs.modal", function () {
            const input = document.getElementById("globalSearchInput");
            if (input) {
                input.value = "";
                input.focus();
            }
            const resultsDiv = document.getElementById("globalSearchResults");
            if (resultsDiv) {
                resultsDiv.classList.add("d-none");
            }
        });
    }

    // Gerçek Zamanlı AJAX Arama Kutusu
    const globalSearchInput = document.getElementById("globalSearchInput");
    if (globalSearchInput) {
        globalSearchInput.addEventListener("input", async function () {
            const query = this.value.trim();
            const resultsDiv = document.getElementById("globalSearchResults");
            const listContainer = document.getElementById("globalSearchResultsList");

            if (query.length < 2) {
                resultsDiv.classList.add("d-none");
                listContainer.innerHTML = "";
                return;
            }

            try {
                const response = await fetch(`/Home/QuickSearch?query=${encodeURIComponent(query)}`);
                const data = await response.json();

                listContainer.innerHTML = "";
                if (data && data.length > 0) {
                    resultsDiv.classList.remove("d-none");
                    data.forEach(item => {
                        const itemEl = document.createElement("a");
                        itemEl.href = `/Products/Edit/${item.id}`;
                        itemEl.className = "d-flex justify-content-between align-items-center p-3 rounded-3 text-decoration-none text-main border-bottom";
                        itemEl.style.borderBottomColor = "var(--border-subtle)";
                        itemEl.style.transition = "background-color 0.2s, padding-left 0.2s";
                        itemEl.style.paddingLeft = "12px";

                        let stockBadge = `<span class="badge bg-success rounded-pill px-2.5 py-1.5 fs-7">${item.quantity} Adet</span>`;
                        if (item.lowStock) {
                            stockBadge = `<span class="badge bg-danger rounded-pill px-2.5 py-1.5 fs-7">${item.quantity} Adet (Kritik)</span>`;
                        }

                        itemEl.innerHTML = `
                            <div class="d-flex flex-column">
                                <span class="fw-semibold text-main fs-6">${item.name}</span>
                                <span class="text-muted small">${item.barcode || 'Barkodsuz'}</span>
                            </div>
                            <div class="d-flex align-items-center gap-2">
                                ${stockBadge}
                                <i class="bi bi-chevron-right text-muted small"></i>
                            </div>
                        `;

                        itemEl.addEventListener("mouseenter", () => {
                            itemEl.style.backgroundColor = "rgba(99, 102, 241, 0.12)";
                            itemEl.style.paddingLeft = "18px";
                        });
                        itemEl.addEventListener("mouseleave", () => {
                            itemEl.style.backgroundColor = "transparent";
                            itemEl.style.paddingLeft = "12px";
                        });

                        listContainer.appendChild(itemEl);
                    });
                } else {
                    resultsDiv.classList.remove("d-none");
                    listContainer.innerHTML = `<div class="text-muted text-center py-4 small"><i class="bi-exclamation-circle d-block fs-3 mb-2 opacity-50"></i>Ürün bulunamadı.</div>`;
                }
            } catch (error) {
                console.error("Hızlı arama hatası:", error);
            }
        });
    }
});
