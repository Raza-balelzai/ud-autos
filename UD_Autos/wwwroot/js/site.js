(function () {
    function initDropdowns() {
        document.querySelectorAll("[data-dropdown]").forEach(function (root) {
            var toggle = root.querySelector("[data-dropdown-toggle]");
            var panel = root.querySelector("[data-dropdown-panel]");
            if (!toggle || !panel) return;

            toggle.addEventListener("click", function (e) {
                e.stopPropagation();
                var open = panel.hidden;
                document.querySelectorAll("[data-dropdown-panel]").forEach(function (p) {
                    p.hidden = true;
                });
                document.querySelectorAll("[data-dropdown-toggle]").forEach(function (b) {
                    b.setAttribute("aria-expanded", "false");
                });
                panel.hidden = !open;
                toggle.setAttribute("aria-expanded", open ? "true" : "false");
            });
        });

        document.addEventListener("click", function () {
            document.querySelectorAll("[data-dropdown-panel]").forEach(function (p) {
                p.hidden = true;
            });
            document.querySelectorAll("[data-dropdown-toggle]").forEach(function (b) {
                b.setAttribute("aria-expanded", "false");
            });
            var searchPanel = document.getElementById("global-search-results");
            if (searchPanel) searchPanel.hidden = true;
        });
    }

    function initGlobalSearch() {
        var root = document.querySelector("[data-global-search]");
        var input = document.getElementById("global-search-input");
        var panel = document.getElementById("global-search-results");
        if (!root || !input || !panel) return;

        var timer = null;
        var lastResults = [];

        function hide() {
            panel.hidden = true;
            panel.innerHTML = "";
        }

        function render(results) {
            lastResults = results || [];
            if (!lastResults.length) {
                panel.innerHTML = '<p class="app-dropdown__empty">No matches</p>';
                panel.hidden = false;
                return;
            }

            var html = '<ul class="app-dropdown__list">';
            lastResults.forEach(function (r) {
                html +=
                    '<li><a class="app-dropdown__item" href="' +
                    encodeURI(r.url) +
                    '" role="option">' +
                    '<span class="app-dropdown__item-type">' +
                    escapeHtml(r.type) +
                    "</span>" +
                    '<span class="app-dropdown__item-title">' +
                    escapeHtml(r.title) +
                    "</span>" +
                    '<span class="app-dropdown__item-detail">' +
                    escapeHtml(r.subtitle || "") +
                    "</span></a></li>";
            });
            html += "</ul>";
            panel.innerHTML = html;
            panel.hidden = false;
        }

        function escapeHtml(s) {
            return String(s)
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;");
        }

        function search(q) {
            if (q.length < 2) {
                hide();
                return;
            }
            fetch("/Search/Suggest?q=" + encodeURIComponent(q), {
                headers: { Accept: "application/json" },
            })
                .then(function (res) {
                    if (!res.ok) throw new Error("search failed");
                    return res.json();
                })
                .then(function (data) {
                    if (input.value.trim() !== q) return;
                    render(data.results || []);
                })
                .catch(function () {
                    panel.innerHTML = '<p class="app-dropdown__empty">Search unavailable</p>';
                    panel.hidden = false;
                });
        }

        input.addEventListener("input", function () {
            clearTimeout(timer);
            var q = input.value.trim();
            timer = setTimeout(function () {
                search(q);
            }, 300);
        });

        input.addEventListener("keydown", function (e) {
            if (e.key === "Escape") {
                hide();
                input.blur();
                return;
            }
            if (e.key === "Enter" && lastResults.length && !panel.hidden) {
                e.preventDefault();
                window.location.href = lastResults[0].url;
            }
        });

        input.addEventListener("click", function (e) {
            e.stopPropagation();
        });
        panel.addEventListener("click", function (e) {
            e.stopPropagation();
        });
    }

    function initNavDrawer() {
        var toggle = document.querySelector("[data-nav-toggle]");
        var backdrop = document.querySelector("[data-nav-backdrop]");
        var sidebar = document.getElementById("app-sidebar");
        if (!toggle || !sidebar) return;

        function setOpen(open) {
            document.body.classList.toggle("is-nav-open", open);
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
            toggle.setAttribute("aria-label", open ? "Close navigation menu" : "Open navigation menu");
            if (backdrop) {
                backdrop.hidden = !open;
                backdrop.setAttribute("aria-hidden", open ? "false" : "true");
            }
        }

        function close() {
            setOpen(false);
        }

        function toggleNav() {
            setOpen(!document.body.classList.contains("is-nav-open"));
        }

        toggle.addEventListener("click", function (e) {
            e.stopPropagation();
            toggleNav();
        });

        if (backdrop) {
            backdrop.addEventListener("click", close);
        }

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && document.body.classList.contains("is-nav-open")) {
                close();
            }
        });

        sidebar.querySelectorAll(".app-nav a").forEach(function (link) {
            link.addEventListener("click", close);
        });

        window.addEventListener("resize", function () {
            if (window.matchMedia("(min-width: 1024px)").matches) {
                close();
            }
        });
    }

    function createToastApi() {
        var host = document.getElementById("app-toast-host");
        if (!host) {
            host = document.createElement("div");
            host.id = "app-toast-host";
            host.className = "toast-host";
            host.setAttribute("aria-live", "polite");
            document.body.appendChild(host);
        }

        var DEFAULT_MS = { success: 4500, error: 6500, info: 4500, warning: 5500 };

        function escapeHtml(s) {
            return String(s)
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;");
        }

        function show(options) {
            var type = (options && options.type) || "info";
            var message = (options && options.message) || "";
            if (!message) return null;

            var duration = options && options.duration != null
                ? options.duration
                : DEFAULT_MS[type] || 4500;

            var el = document.createElement("div");
            el.className = "toast toast--" + type;
            el.setAttribute("role", type === "error" ? "alert" : "status");
            el.innerHTML =
                '<div class="toast__body">' +
                '<p class="toast__message">' +
                escapeHtml(message) +
                "</p>" +
                '<button type="button" class="toast__close" aria-label="Dismiss notification">&times;</button>' +
                "</div>" +
                '<div class="toast__progress" aria-hidden="true"><span class="toast__progress-bar"></span></div>';

            host.appendChild(el);
            requestAnimationFrame(function () {
                el.classList.add("is-visible");
            });

            var remaining = duration;
            var startedAt = 0;
            var timerId = null;
            var stuck = false;
            var closed = false;
            var progressBar = el.querySelector(".toast__progress-bar");

            function clearTimer() {
                if (timerId) {
                    clearTimeout(timerId);
                    timerId = null;
                }
            }

            function updateProgress(running) {
                if (!progressBar) return;
                if (stuck || duration <= 0) {
                    progressBar.style.transition = "none";
                    progressBar.style.transform = "scaleX(0)";
                    return;
                }
                if (running) {
                    progressBar.style.transition = "transform " + remaining + "ms linear";
                    progressBar.style.transform = "scaleX(0)";
                } else {
                    var computed = window.getComputedStyle(progressBar).transform;
                    progressBar.style.transition = "none";
                    progressBar.style.transform = computed;
                }
            }

            function startTimer() {
                if (stuck || closed || duration <= 0) return;
                startedAt = Date.now();
                if (progressBar) {
                    // reset width to remaining fraction then animate to 0
                    var fraction = Math.max(0, Math.min(1, remaining / duration));
                    progressBar.style.transition = "none";
                    progressBar.style.transform = "scaleX(" + fraction + ")";
                    // force reflow
                    void progressBar.offsetWidth;
                }
                updateProgress(true);
                timerId = setTimeout(dismiss, remaining);
            }

            function pauseTimer() {
                if (stuck || closed || !timerId) return;
                clearTimer();
                remaining = Math.max(0, remaining - (Date.now() - startedAt));
                updateProgress(false);
            }

            function dismiss() {
                if (closed) return;
                closed = true;
                clearTimer();
                el.classList.remove("is-visible");
                el.classList.add("is-leaving");
                setTimeout(function () {
                    if (el.parentNode) el.parentNode.removeChild(el);
                }, 220);
            }

            function stick() {
                if (closed) return;
                stuck = true;
                clearTimer();
                el.classList.add("is-stuck");
                updateProgress(false);
            }

            el.querySelector(".toast__close").addEventListener("click", function (e) {
                e.stopPropagation();
                dismiss();
            });

            el.addEventListener("mouseenter", pauseTimer);
            el.addEventListener("mouseleave", function () {
                if (!stuck) startTimer();
            });

            el.addEventListener("click", function (e) {
                if (e.target.closest(".toast__close")) return;
                stick();
            });

            if (duration > 0) {
                // set initial full bar then start
                if (progressBar) {
                    progressBar.style.transform = "scaleX(1)";
                }
                startTimer();
            }

            return { dismiss: dismiss, stick: stick, el: el };
        }

        function readBootMessage(id) {
            var node = document.getElementById(id);
            if (!node) return null;
            try {
                return JSON.parse(node.textContent || "null");
            } catch (e) {
                return node.textContent;
            } finally {
                node.remove();
            }
        }

        function bootFromTempData() {
            var success = readBootMessage("toast-data-success");
            var error = readBootMessage("toast-data-error");
            if (success) show({ type: "success", message: success });
            if (error) show({ type: "error", message: error });
        }

        return { show: show, bootFromTempData: bootFromTempData };
    }

    var AppToast = createToastApi();
    window.AppToast = AppToast;

    function initConfirmModal() {
        var root = document.getElementById("app-confirm");
        if (!root) return;
        var messageEl = document.getElementById("app-confirm-message");
        var okBtn = document.getElementById("app-confirm-ok");
        var pending = null;

        function close() {
            root.hidden = true;
            document.body.classList.remove("is-modal-open");
            pending = null;
        }

        function open(message, tone, onOk) {
            messageEl.textContent = message;
            okBtn.className = "btn-app " + (tone === "success" ? "btn-app--success" : "btn-app--danger");
            okBtn.textContent = tone === "success" ? "Continue" : "Confirm";
            pending = onOk;
            root.hidden = false;
            document.body.classList.add("is-modal-open");
            okBtn.focus();
        }

        root.querySelectorAll("[data-confirm-cancel]").forEach(function (el) {
            el.addEventListener("click", close);
        });
        okBtn.addEventListener("click", function () {
            var run = pending;
            close();
            if (run) run();
        });
        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && !root.hidden) close();
        });

        document.addEventListener("submit", function (e) {
            var form = e.target;
            if (!form || !form.getAttribute) return;
            if (form.getAttribute("data-confirmed") === "1") {
                form.removeAttribute("data-confirmed");
                return;
            }
            var btn = e.submitter;
            var message = (btn && btn.getAttribute("data-confirm")) || form.getAttribute("data-confirm");
            if (!message) return;
            e.preventDefault();
            open(message, btn && btn.getAttribute("data-confirm-tone"), function () {
                form.setAttribute("data-confirmed", "1");
                if (typeof form.requestSubmit === "function") form.requestSubmit(btn || undefined);
                else form.submit();
            });
        });
    }

    function initImagePreview() {
        var root = document.getElementById("app-image-preview");
        if (!root) return;
        var img = document.getElementById("app-preview-image");
        var title = document.getElementById("app-preview-title");

        function close() {
            root.hidden = true;
            img.removeAttribute("src");
            var confirmModal = document.getElementById("app-confirm");
            if (!confirmModal || confirmModal.hidden) document.body.classList.remove("is-modal-open");
        }

        function open(src, name) {
            if (!src) return;
            img.src = src;
            img.alt = name || "Image preview";
            title.textContent = name || "Image";
            root.hidden = false;
            document.body.classList.add("is-modal-open");
            var closeBtn = root.querySelector("[data-preview-close].btn-app");
            if (closeBtn) closeBtn.focus();
        }

        document.addEventListener("click", function (e) {
            var trigger = e.target.closest("[data-image-preview]");
            if (!trigger) return;
            e.preventDefault();
            open(trigger.getAttribute("data-image-preview"), trigger.getAttribute("data-image-title"));
        });

        root.querySelectorAll("[data-preview-close]").forEach(function (el) {
            el.addEventListener("click", close);
        });

        document.addEventListener("keydown", function (e) {
            if (e.key === "Escape" && !root.hidden) close();
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        initDropdowns();
        initGlobalSearch();
        initNavDrawer();
        initConfirmModal();
        initImagePreview();
        AppToast.bootFromTempData();
    });
})();
