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

    document.addEventListener("DOMContentLoaded", function () {
        initDropdowns();
        initGlobalSearch();
        initNavDrawer();
    });
})();
