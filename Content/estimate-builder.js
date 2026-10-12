// Shared "add product" attribute picker for the storefront and admin estimate pages.
// Fields are posted as product_attribute_{mappingId}, the names nopCommerce's product page uses,
// so the server can parse them with IProductAttributeParser.ParseProductAttributesAsync.
window.EstimateBuilder = (function () {
    var CONTROL = { dropdown: 1, radio: 2, checkboxes: 3, textbox: 4, multiline: 10, datepicker: 20, fileUpload: 30, colorSquares: 40, imageSquares: 45, readonlyCheckboxes: 50 };

    function esc(value) {
        return String(value == null ? '' : value).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function valueLabel(v, showAdjustments) {
        return esc(v.name) + (showAdjustments && v.priceAdjustmentStr ? ' (' + esc(v.priceAdjustmentStr) + ')' : '');
    }

    // css: { group, label, select, input, textarea, option } class names for the host page's styling
    function renderAttributes(panel, attributes, css, showAdjustments) {
        css = css || {};
        var html = '<strong>Configure Options:</strong><div class="' + esc(css.group) + '">';

        attributes.forEach(function (attr) {
            var name = 'product_attribute_' + attr.mappingId;
            html += '<div data-attr-block="' + attr.mappingId + '">';
            html += '<label class="' + esc(css.label) + '">' + esc(attr.name) + (attr.isRequired ? ' <span style="color:#c0392b">*</span>' : '') + '</label><br/>';

            switch (attr.controlType) {
                case CONTROL.dropdown:
                    html += '<select name="' + name + '" class="' + esc(css.select) + '">';
                    if (!attr.isRequired) html += '<option value="">-- None --</option>';
                    attr.values.forEach(function (v) {
                        html += '<option value="' + v.id + '"' + (v.isPreSelected ? ' selected' : '') + '>' + valueLabel(v, showAdjustments) + '</option>';
                    });
                    html += '</select>';
                    break;

                case CONTROL.radio:
                case CONTROL.colorSquares:
                case CONTROL.imageSquares:
                    attr.values.forEach(function (v) {
                        var swatch = (attr.controlType === CONTROL.colorSquares && /^#?[0-9a-fA-F]{3,8}$/.test(v.colorSquaresRgb))
                            ? '<span style="display:inline-block;width:12px;height:12px;background:' + esc(v.colorSquaresRgb) + ';border:1px solid #999;margin-right:3px;vertical-align:middle;border-radius:2px;"></span>'
                            : '';
                        html += '<label class="' + esc(css.option) + '"><input type="radio" name="' + name + '" value="' + v.id + '"' + (v.isPreSelected ? ' checked' : '') + '> ' + swatch + valueLabel(v, showAdjustments) + '</label>';
                    });
                    break;

                case CONTROL.checkboxes:
                case CONTROL.readonlyCheckboxes:
                    // Read-only checkboxes are applied server-side from their pre-selected values
                    var readonly = attr.controlType === CONTROL.readonlyCheckboxes;
                    attr.values.forEach(function (v) {
                        html += '<label class="' + esc(css.option) + '"><input type="checkbox"' + (readonly ? ' disabled' : ' name="' + name + '"') + ' value="' + v.id + '"' + (v.isPreSelected ? ' checked' : '') + '> ' + valueLabel(v, showAdjustments) + '</label>';
                    });
                    break;

                case CONTROL.multiline:
                    html += '<textarea name="' + name + '" rows="2" class="' + esc(css.textarea) + '">' + esc(attr.defaultValue) + '</textarea>';
                    break;

                case CONTROL.datepicker:
                    html += '<input type="date" data-date-for="' + name + '" class="' + esc(css.input) + '" />';
                    break;

                case CONTROL.fileUpload:
                    html += '<span style="font-size:12px;color:#777;">File uploads can only be added from the product page.</span>';
                    break;

                default:
                    html += '<input type="text" name="' + name + '" value="' + esc(attr.defaultValue) + '" class="' + esc(css.input) + '" />';
            }

            html += '</div>';
        });

        panel.innerHTML = html + '</div>';
        panel.style.display = 'block';
    }

    // Appends the panel's selections to a URLSearchParams body
    function appendAttributes(panel, params) {
        if (panel.style.display === 'none') return;

        panel.querySelectorAll('[name^="product_attribute_"]').forEach(function (el) {
            if ((el.type === 'radio' || el.type === 'checkbox') && !el.checked) return;
            if (el.value !== '') params.append(el.name, el.value);
        });

        // The parser expects date pickers as separate _day/_month/_year fields
        panel.querySelectorAll('[data-date-for]').forEach(function (el) {
            if (!el.value) return;
            var parts = el.value.split('-');
            var name = el.getAttribute('data-date-for');
            params.append(name + '_year', parts[0]);
            params.append(name + '_month', String(parseInt(parts[1], 10)));
            params.append(name + '_day', String(parseInt(parts[2], 10)));
        });
    }

    // Fills the product <select> from the category and/or search box. Typing is debounced, and a slow
    // older response can't overwrite a newer one.
    function productPicker(options) {
        var categorySelect = document.getElementById(options.categoryId);
        var searchInput = document.getElementById(options.searchId);
        var productSelect = document.getElementById(options.productId);
        var timer = null, requestNo = 0;

        function setPlaceholder(text) {
            productSelect.innerHTML = '';
            var opt = document.createElement('option');
            opt.value = '';
            opt.textContent = text;
            productSelect.appendChild(opt);
        }

        function load() {
            var catId = categorySelect.value;
            var q = searchInput.value.trim();
            if (options.onReset) options.onReset();

            if (!catId && q.length < 2) {
                setPlaceholder('-- Choose a category or search --');
                productSelect.disabled = true;
                return;
            }

            var current = ++requestNo;
            setPlaceholder('-- Loading... --');
            productSelect.disabled = true;

            fetch(options.url + '?categoryId=' + encodeURIComponent(catId || 0) + '&q=' + encodeURIComponent(q))
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (current !== requestNo) return;
                    setPlaceholder(data.length ? '-- Select Product (' + data.length + ') --' : '-- No products found --');
                    data.forEach(function (p) {
                        var opt = document.createElement('option');
                        opt.value = p.id;
                        opt.textContent = p.name;
                        productSelect.appendChild(opt);
                    });
                    productSelect.disabled = data.length === 0;
                });
        }

        categorySelect.addEventListener('change', load);
        searchInput.addEventListener('input', function () {
            clearTimeout(timer);
            timer = setTimeout(load, 300);
        });
        searchInput.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); clearTimeout(timer); load(); }
        });
        setPlaceholder('-- Choose a category or search --');
    }

    return { esc: esc, renderAttributes: renderAttributes, appendAttributes: appendAttributes, productPicker: productPicker };
})();
