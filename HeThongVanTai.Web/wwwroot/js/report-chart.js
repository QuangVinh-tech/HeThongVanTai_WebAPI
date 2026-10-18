// Vẽ biểu đồ doanh thu cho các trang báo cáo. Dữ liệu nằm trong <script id="chartData" type="application/json">.
// kind: "line" (theo ngày), "bar" (theo tháng), "bar-h" (thanh ngang: tuyến, xe, tài xế, nhân viên).
(function () {
    var dataEl = document.getElementById('chartData');
    var canvas = document.getElementById('reportChart');
    if (!dataEl || !canvas) { return; }

    var card = canvas.closest('.chart-card');
    if (typeof Chart === 'undefined') {            // thư viện biểu đồ không tải được (mất mạng): ẩn khung, bảng số liệu vẫn đủ dùng
        if (card) { card.hidden = true; }
        return;
    }

    var d = JSON.parse(dataEl.textContent);
    var nf = new Intl.NumberFormat('vi-VN');
    var horizontal = d.kind === 'bar-h';
    var valueAxis = { ticks: { callback: function (v) { return nf.format(v); } } };

    new Chart(canvas, {
        type: d.kind === 'line' ? 'line' : 'bar',
        data: {
            labels: d.labels,
            datasets: [{
                label: 'Doanh thu (đ)',
                data: d.values,
                backgroundColor: 'rgba(176, 48, 96, 0.75)',
                borderColor: '#B03060',
                borderWidth: d.kind === 'line' ? 2 : 1,
                pointRadius: d.labels.length > 45 ? 0 : 3,
                tension: 0.25,
                fill: false
            }]
        },
        options: {
            indexAxis: horizontal ? 'y' : 'x',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (c) {
                            var v = horizontal ? c.parsed.x : c.parsed.y;
                            return nf.format(v) + ' đ';
                        }
                    }
                }
            },
            scales: horizontal ? { x: valueAxis } : { y: valueAxis }
        }
    });
})();
