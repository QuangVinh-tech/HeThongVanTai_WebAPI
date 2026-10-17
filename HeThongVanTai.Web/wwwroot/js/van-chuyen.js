// Quản lý danh sách kiện hàng + gọi API tính cước. Dùng chung cho trang Tính cước và Tạo đơn.
(function () {
    'use strict';

    const FIELDS = ['goodsType', 'name', 'weight', 'length', 'width', 'height', 'quantity'];
    const NUMERIC_BLANK_WHEN_ZERO = ['weight', 'length', 'width', 'height'];
    const pascal = f => f.charAt(0).toUpperCase() + f.slice(1);
    const num = v => parseFloat(String(v).replace(',', '.')) || 0;

    const list = document.getElementById('parcelList');
    const tpl = document.getElementById('parcelTpl');
    if (!list || !tpl) return;

    const rows = () => Array.from(list.children);
    const field = (row, f) => row.querySelector('[data-f="' + f + '"]');

    function readRow(row) {
        const d = {};
        FIELDS.forEach(f => { d[f] = field(row, f).value; });
        return d;
    }

    function updateVolumetric(row) {
        const v = num(field(row, 'length').value) * num(field(row, 'width').value) * num(field(row, 'height').value) / 5000;
        field(row, 'vol').value = v.toFixed(2);
    }

    // Đánh lại tên input Parcels[i].Field để MVC bind được thành List<ParcelForm>
    function reindex() {
        const all = rows();
        all.forEach((row, i) => {
            row.querySelector('[data-role="title"]').textContent = 'Kiện hàng ' + (i + 1);
            FIELDS.forEach(f => { field(row, f).name = 'Parcels[' + i + '].' + pascal(f); });
            row.querySelector('[data-act="remove"]').disabled = all.length === 1;
        });
    }

    function addRow(data) {
        const row = tpl.content.firstElementChild.cloneNode(true);
        list.appendChild(row);

        if (data) {
            FIELDS.forEach(f => {
                const el = field(row, f);
                let v = data[f];
                if (v === undefined || v === null) return;
                if (NUMERIC_BLANK_WHEN_ZERO.includes(f) && Number(v) === 0) v = '';
                el.value = v;   // gán qua .value, không dùng innerHTML nên không bị chèn mã
            });
        }

        row.querySelector('[data-act="remove"]').addEventListener('click', () => {
            if (rows().length > 1) { row.remove(); reindex(); }
        });
        row.querySelector('[data-act="copy"]').addEventListener('click', () => addRow(readRow(row)));
        ['length', 'width', 'height'].forEach(f =>
            field(row, f).addEventListener('input', () => updateVolumetric(row)));

        reindex();
        updateVolumetric(row);
    }

    function gatherParcels() {
        return rows().map(row => {
            const d = readRow(row);
            return {
                goodsType: d.goodsType,
                name: (d.name || '').trim() || 'Hàng hóa',
                weight: num(d.weight),
                length: parseInt(d.length, 10) || 0,
                width: parseInt(d.width, 10) || 0,
                height: parseInt(d.height, 10) || 0,
                quantity: parseInt(d.quantity, 10) || 1
            };
        });
    }

    async function quote(body) {
        try {
            const res = await fetch('/Shipments/QuoteAjax', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            const data = await res.json().catch(() => ({}));
            return res.ok ? { ok: true, data } : { ok: false, message: data.message || 'Không tính được cước.' };
        } catch (e) {
            return { ok: false, message: 'Không kết nối được máy chủ, vui lòng thử lại.' };
        }
    }

    const money = n => Number(n || 0).toLocaleString('vi-VN') + ' đ';

    document.getElementById('btnAddParcel').addEventListener('click', () => addRow());

    let initial = [];
    try { initial = JSON.parse(document.getElementById('parcelInit').textContent) || []; } catch (e) { }
    if (initial.length === 0) addRow(); else initial.forEach(addRow);

    window.VanChuyen = { gatherParcels, quote, money };
})();