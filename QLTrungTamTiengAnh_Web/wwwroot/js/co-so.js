(function () {
    'use strict';

    const script = document.currentScript;
    const page = document.getElementById('coSoPage');
    if (!page) return;

    // Use the bundled Bootstrap if the layout's CDN is unavailable.
    if (!window.bootstrap) {
        const fallback = document.createElement('script');
        fallback.src = script.dataset.bootstrapUrl;
        fallback.onload = initialize;
        fallback.onerror = function () {
            const message = document.getElementById('pageMessage');
            message.className = 'alert alert-danger';
            message.textContent = 'Không tải được thư viện giao diện. Vui lòng tải lại trang.';
        };
        document.head.appendChild(fallback);
    } else {
        initialize();
    }

    function initialize() {
        const byId = id => document.getElementById(id);
        const state = { coSos: [], phongHocs: [], nhanSus: [], dieuChuyens: [], loaded: false, loading: false, busy: false, confirming: false };
        let allowModalClose = false;
        const branchModal = bootstrap.Modal.getOrCreateInstance(byId('modalBranch'));
        const roomModal = bootstrap.Modal.getOrCreateInstance(byId('modalRoom'));
        const transferModal = bootstrap.Modal.getOrCreateInstance(byId('modalTransfer'));
        const token = page.querySelector('input[name="__RequestVerificationToken"]').value;
        const escape = value => String(value == null ? '' : value).replace(/[&<>"']/g, character => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        })[character]);
        const findBranch = id => state.coSos.find(branch => branch.coSoId === Number(id));
        const findStaff = () => state.nhanSus.find(staff => staff.nhanSuId === Number(byId('selectTransferStaff').value));
        const eligibleStaff = () => state.nhanSus.filter(staff => findBranch(staff.coSoId));
        const branchName = id => { const branch = findBranch(id); return branch ? branch.tenCoSo : 'Chưa phân cơ sở'; };
        const badge = (value, color) => `<span class="badge bg-${color}-subtle text-${color} border">${escape(value)}</span>`;
        const emptyRow = (columns, message) => `<tr><td colspan="${columns}" class="text-center text-muted py-4">${escape(message)}</td></tr>`;

        function showMessage(id, message, type) {
            const element = byId(id);
            element.textContent = message || '';
            element.className = message ? `alert alert-${type || 'danger'}` : 'alert d-none';
            if (message && id !== 'pageMessage') element.focus();
        }

        function syncControls() {
            const disabled = state.loading || state.busy || state.confirming || !state.loaded;
            document.querySelectorAll('[data-mutation]').forEach(button => { button.disabled = disabled; });
            byId('btnOpenCreateRoom').disabled = disabled || !state.coSos.some(branch => branch.trangThai);
            byId('btnOpenTransferModal').disabled = disabled || !eligibleStaff().length || !state.coSos.some(branch => branch.trangThai);
            byId('btnExecuteTransfer').disabled = disabled || !findStaff() || !byId('selectNewBranch').value;
            byId('btnRefreshData').disabled = state.loading || state.busy || state.confirming;
            byId('branchTabContent').setAttribute('aria-busy', String(state.loading || state.busy));
            document.querySelectorAll('.modal').forEach(modal => {
                modal.querySelectorAll('input, select, textarea, [data-bs-dismiss]').forEach(control => {
                    control.disabled = state.busy;
                });
            });
        }

        async function request(url, payload) {
            let response;
            try {
                response = await fetch(url, {
                    method: payload === undefined ? 'GET' : 'POST',
                    credentials: 'same-origin',
                    headers: payload === undefined ? { 'Accept': 'application/json' } : {
                        'Accept': 'application/json', 'Content-Type': 'application/json', 'RequestVerificationToken': token
                    },
                    body: payload === undefined ? undefined : JSON.stringify(payload)
                });
            } catch (_) {
                throw new Error('Không kết nối được máy chủ. Vui lòng kiểm tra kết nối và thử lại.');
            }
            if (response.status === 401 || (response.redirected && /\/Account\/Login(?:[/?]|$)/i.test(response.url))) {
                throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại rồi tải lại trang.');
            }
            if (response.status === 403 || (response.redirected && /\/Account\/AccessDenied(?:[/?]|$)/i.test(response.url))) {
                throw new Error('Bạn không có quyền quản lý cơ sở, phòng học và điều chuyển nhân sự.');
            }
            let data;
            try { data = await response.json(); } catch (_) {
                throw new Error(response.status === 400
                    ? 'Yêu cầu không hợp lệ hoặc phiên xác thực đã hết hạn. Vui lòng tải lại trang và thử lại.'
                    : 'Máy chủ trả về dữ liệu không hợp lệ. Vui lòng tải lại trang hoặc thử lại sau.');
            }
            if (!response.ok || !data || data.success !== true) {
                const validation = data && data.errors ? Object.values(data.errors).flat().join(' ') : '';
                throw new Error((data && data.message) || validation || 'Không thực hiện được yêu cầu. Vui lòng thử lại.');
            }
            return data;
        }

        function renderBranches() {
            if (!state.loaded) return;
            const keyword = byId('branchSearch').value.trim().toLocaleLowerCase('vi');
            const branches = state.coSos.filter(branch => [branch.maCoSo, branch.tenCoSo, branch.diaChi]
                .some(value => String(value || '').toLocaleLowerCase('vi').includes(keyword)));
            byId('branchTableBody').innerHTML = branches.map(branch => `<tr>
                <td class="ps-3">${badge(branch.maCoSo, 'primary')}</td>
                <td class="fw-bold text-dark">${escape(branch.tenCoSo)}</td>
                <td>${escape(branch.diaChi)}</td><td>${escape(branch.soDienThoai || '—')}</td>
                <td>${badge(`${branch.soPhong} Phòng học • ${branch.soNhanSu} Nhân sự`, 'info')}</td>
                <td>${badge(branch.trangThai ? 'Hoạt động' : 'Tạm ngưng', branch.trangThai ? 'success' : 'secondary')}</td>
                <td class="text-end pe-3 text-nowrap">
                    <button type="button" class="btn btn-sm btn-outline-info me-1" data-action="branch-rooms" data-id="${escape(branch.coSoId)}" title="Xem phòng học" aria-label="Xem phòng học"><i class="fa-solid fa-door-open" aria-hidden="true"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-primary me-1" data-action="edit-branch" data-id="${escape(branch.coSoId)}" data-mutation title="Sửa cơ sở" aria-label="Sửa cơ sở"><i class="fa-solid fa-pen" aria-hidden="true"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-${branch.trangThai ? 'danger' : 'success'}" data-action="status-branch" data-id="${escape(branch.coSoId)}" data-mutation title="${branch.trangThai ? 'Tạm ngưng' : 'Kích hoạt'} cơ sở" aria-label="${branch.trangThai ? 'Tạm ngưng' : 'Kích hoạt'} cơ sở"><i class="fa-solid fa-${branch.trangThai ? 'ban' : 'circle-check'}" aria-hidden="true"></i></button>
                </td></tr>`).join('') || emptyRow(7, keyword ? 'Không có cơ sở phù hợp với từ khóa.' : 'Chưa có cơ sở. Chọn “Thêm Cơ Sở Mới” để bắt đầu.');
            syncControls();
        }

        function renderRooms() {
            if (!state.loaded) return;
            const filter = Number(byId('roomBranchFilter').value);
            const rooms = state.phongHocs.filter(room => !filter || room.coSoId === filter);
            byId('roomTableBody').innerHTML = rooms.map(room => `<tr>
                <td class="ps-3 fw-semibold">${escape(room.tenPhong)}</td>
                <td>${escape(room.tenCoSo || branchName(room.coSoId))}</td>
                <td>${escape(room.sucChua)} người</td>
                <td>${badge(room.trangThai || 'Chưa xác định', room.trangThai === 'Đang sử dụng' ? 'success' : room.trangThai === 'Bảo trì' ? 'warning' : 'secondary')}</td>
                <td class="text-break">${escape(room.ghiChu || '—')}</td>
                <td class="text-end pe-3 text-nowrap">
                    <button type="button" class="btn btn-sm btn-outline-primary me-1" data-action="edit-room" data-id="${escape(room.phongHocId)}" data-mutation title="Sửa phòng học" aria-label="Sửa phòng học"><i class="fa-solid fa-pen" aria-hidden="true"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-danger" data-action="delete-room" data-id="${escape(room.phongHocId)}" data-mutation title="Xóa phòng học" aria-label="Xóa phòng học"><i class="fa-solid fa-trash" aria-hidden="true"></i></button>
                </td></tr>`).join('') || emptyRow(6, filter ? 'Cơ sở này chưa có phòng học.' : 'Chưa có phòng học. Hãy thêm cơ sở và phòng học.');
            syncControls();
        }

        function formatDate(value) {
            if (!value) return '—';
            const date = new Date(value);
            return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString('vi-VN', {
                year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
            });
        }

        function renderTransfers() {
            byId('transferTableBody').innerHTML = state.dieuChuyens.map(transfer => `<tr>
                <td class="ps-3">${badge(`DC-${transfer.dieuChuyenId}`, 'secondary')}</td>
                <td class="fw-semibold">${escape(transfer.hoTen)}</td>
                <td>${escape(transfer.coSoCu || 'Chưa phân cơ sở')}</td><td>${escape(transfer.coSoMoi)}</td>
                <td class="text-break">${escape(transfer.lyDo)}</td><td>${escape(transfer.nguoiDuyet || '—')}</td>
                <td class="text-nowrap">${escape(formatDate(transfer.ngayHieuLuc))}</td>
                <td>${badge(transfer.trangThai || 'Chưa xác định', transfer.trangThai === 'Đã duyệt' || transfer.trangThai === 'Đã phê duyệt' ? 'success' : 'secondary')}</td>
                </tr>`).join('') || emptyRow(8, 'Chưa có lịch sử điều chuyển nhân sự.');
        }

        function setOptions(select, items, prompt, selected) {
            select.replaceChildren(new Option(prompt, ''));
            items.forEach(item => select.add(new Option(item.label, String(item.id))));
            if (selected != null && items.some(item => String(item.id) === String(selected))) select.value = String(selected);
        }

        function updateTransferBranches() {
            const staff = findStaff();
            byId('txtCurrentBranch').value = staff ? (staff.tenCoSo || branchName(staff.coSoId)) : '';
            const select = byId('selectNewBranch');
            const selected = select.value;
            const branches = state.coSos.filter(branch => branch.trangThai && (!staff || branch.coSoId !== staff.coSoId));
            setOptions(select, branches.map(branch => ({ id: branch.coSoId, label: branch.tenCoSo })), '-- Chọn cơ sở đến --', selected);
            byId('transferHelp').textContent = staff && !branches.length
                ? 'Không có cơ sở đang hoạt động khác để điều chuyển nhân sự này.'
                : 'Chỉ chọn cơ sở đang hoạt động, khác cơ sở hiện tại.';
            syncControls();
        }

        function renderOptions() {
            const filter = byId('roomBranchFilter');
            setOptions(filter, state.coSos.map(branch => ({ id: branch.coSoId, label: branch.tenCoSo + (branch.trangThai ? '' : ' (Tạm ngưng)') })), 'Tất cả cơ sở', filter.value);
            const staff = byId('selectTransferStaff');
            setOptions(staff, eligibleStaff().map(person => ({ id: person.nhanSuId, label: `${person.hoTen} — ${person.tenCoSo || branchName(person.coSoId)}` })), '-- Chọn nhân sự điều chuyển --', staff.value);
            updateTransferBranches();
        }

        async function loadData() {
            if (state.loading) return false;
            state.loading = true;
            byId('dataStatus').textContent = 'Đang tải dữ liệu...';
            syncControls();
            try {
                const data = await request(page.dataset.dataUrl);
                if (!['coSos', 'phongHocs', 'nhanSus', 'dieuChuyens'].every(key => Array.isArray(data[key]))) {
                    throw new Error('Dữ liệu danh sách chưa đầy đủ. Vui lòng thử tải lại.');
                }
                state.coSos = data.coSos;
                state.phongHocs = data.phongHocs;
                state.nhanSus = data.nhanSus;
                state.dieuChuyens = data.dieuChuyens;
                state.loaded = true;
                renderOptions();
                renderBranches();
                renderRooms();
                renderTransfers();
                byId('dataStatus').textContent = `${state.coSos.length} cơ sở • ${state.phongHocs.length} phòng học • ${state.dieuChuyens.length} lượt điều chuyển`;
                return true;
            } catch (error) {
                state.loaded = false;
                showMessage('pageMessage', error.message, 'danger');
                byId('dataStatus').textContent = 'Chưa tải được dữ liệu. Chọn “Tải Lại” để thử lại.';
                byId('branchTableBody').innerHTML = emptyRow(7, 'Không tải được danh sách cơ sở.');
                byId('roomTableBody').innerHTML = emptyRow(6, 'Không tải được danh sách phòng học.');
                byId('transferTableBody').innerHTML = emptyRow(8, 'Không tải được lịch sử điều chuyển.');
                return false;
            } finally {
                state.loading = false;
                syncControls();
            }
        }

        async function mutate(url, payload, modal, errorId, successMessage, afterSave) {
            if (state.busy || state.loading || state.confirming || !state.loaded) return;
            state.busy = true;
            showMessage(errorId, '');
            byId('dataStatus').textContent = 'Đang lưu thay đổi...';
            syncControls();
            try {
                const result = await request(url, payload);
                if (modal) {
                    allowModalClose = true;
                    modal.hide();
                    allowModalClose = false;
                }
                const refreshed = await loadData();
                if (refreshed) {
                    showMessage('pageMessage', result.message || successMessage, 'success');
                    if (afterSave) afterSave();
                } else {
                    const refreshError = byId('pageMessage').textContent;
                    showMessage('pageMessage', `${result.message || successMessage} Tuy nhiên chưa tải lại được danh sách. ${refreshError}`, 'warning');
                }
            } catch (error) {
                showMessage(errorId, error.message, 'danger');
                byId('dataStatus').textContent = 'Chưa lưu được thay đổi. Vui lòng xem thông báo lỗi và thử lại.';
            } finally {
                state.busy = false;
                syncControls();
            }
        }

        function validated(formId) {
            const form = byId(formId);
            form.querySelectorAll('input[type="text"], input[type="tel"], textarea').forEach(input => { input.value = input.value.trim(); });
            return form.reportValidity();
        }

        function openBranch(branch) {
            byId('formBranch').reset();
            showMessage('branchError', '');
            byId('hiddenBranchId').value = branch ? branch.coSoId : 0;
            byId('txtBranchCode').value = branch ? branch.maCoSo : '';
            byId('txtBranchName').value = branch ? branch.tenCoSo : '';
            byId('txtBranchAddress').value = branch ? branch.diaChi : '';
            byId('txtBranchPhone').value = branch ? branch.soDienThoai || '' : '';
            byId('modalBranchTitle').textContent = branch ? 'Chỉnh Sửa Cơ Sở' : 'Thêm Cơ Sở Đào Tạo Mới';
            branchModal.show();
        }

        function openRoom(room) {
            byId('formRoom').reset();
            showMessage('roomError', '');
            byId('hiddenRoomId').value = room ? room.phongHocId : 0;
            // Retain the current branch when editing a room at a suspended branch.
            const available = state.coSos.filter(branch => branch.trangThai || (room && branch.coSoId === room.coSoId));
            setOptions(byId('selectRoomBranch'), available.map(branch => ({ id: branch.coSoId, label: branch.tenCoSo + (branch.trangThai ? '' : ' (Tạm ngưng)') })), '-- Chọn cơ sở --', room ? room.coSoId : byId('roomBranchFilter').value);
            byId('txtRoomName').value = room ? room.tenPhong : '';
            byId('txtRoomCapacity').value = room ? room.sucChua : '';
            byId('selectRoomStatus').value = room ? room.trangThai : 'Đang sử dụng';
            byId('txtRoomNote').value = room ? room.ghiChu || '' : '';
            byId('modalRoomTitle').textContent = room ? 'Chỉnh Sửa Phòng Học' : 'Thêm Phòng Học';
            roomModal.show();
        }

        async function confirmAction(title, message, confirmText) {
            state.confirming = true;
            syncControls();
            try {
                if (window.Swal && typeof window.Swal.fire === 'function') {
                    const result = await window.Swal.fire({ titleText: title, text: message, icon: 'warning', showCancelButton: true,
                        confirmButtonText: confirmText, cancelButtonText: 'Hủy', confirmButtonColor: '#0d6efd' });
                    return result.isConfirmed;
                }
                return window.confirm(`${title}\n${message}`);
            } finally {
                state.confirming = false;
                syncControls();
            }
        }

        byId('btnOpenCreateBranch').addEventListener('click', () => openBranch());
        byId('btnOpenCreateRoom').addEventListener('click', () => openRoom());
        byId('branchSearch').addEventListener('input', renderBranches);
        byId('roomBranchFilter').addEventListener('change', renderRooms);
        byId('btnRefreshData').addEventListener('click', () => { showMessage('pageMessage', ''); loadData(); });
        byId('selectTransferStaff').addEventListener('change', updateTransferBranches);
        byId('selectNewBranch').addEventListener('change', syncControls);

        byId('btnOpenTransferModal').addEventListener('click', function () {
            byId('formTransfer').reset();
            showMessage('transferError', '');
            updateTransferBranches();
            transferModal.show();
        });

        byId('formBranch').addEventListener('submit', function (event) {
            event.preventDefault();
            if (!validated('formBranch')) return;
            mutate(page.dataset.saveBranchUrl, {
                coSoId: Number(byId('hiddenBranchId').value), maCoSo: byId('txtBranchCode').value,
                tenCoSo: byId('txtBranchName').value, diaChi: byId('txtBranchAddress').value,
                soDienThoai: byId('txtBranchPhone').value
            }, branchModal, 'branchError', 'Đã lưu thông tin cơ sở.');
        });

        byId('formRoom').addEventListener('submit', function (event) {
            event.preventDefault();
            if (!validated('formRoom')) return;
            mutate(page.dataset.saveRoomUrl, {
                phongHocId: Number(byId('hiddenRoomId').value), coSoId: Number(byId('selectRoomBranch').value),
                tenPhong: byId('txtRoomName').value, sucChua: Number(byId('txtRoomCapacity').value),
                trangThai: byId('selectRoomStatus').value, ghiChu: byId('txtRoomNote').value
            }, roomModal, 'roomError', 'Đã lưu thông tin phòng học.');
        });

        byId('formTransfer').addEventListener('submit', function (event) {
            event.preventDefault();
            if (!validated('formTransfer')) return;
            const staff = findStaff();
            const target = findBranch(byId('selectNewBranch').value);
            if (!staff || !findBranch(staff.coSoId) || !target || !target.trangThai || staff.coSoId === target.coSoId) {
                showMessage('transferError', 'Vui lòng chọn nhân sự có cơ sở hợp lệ và cơ sở đang hoạt động khác cơ sở hiện tại.');
                return;
            }
            mutate(page.dataset.transferUrl, {
                nhanSuId: staff.nhanSuId, coSoHienTaiId: staff.coSoId,
                coSoMoiId: target.coSoId, lyDo: byId('txtTransferReason').value
            }, transferModal, 'transferError', 'Đã phê duyệt điều chuyển nhân sự.', () => {
                bootstrap.Tab.getOrCreateInstance(byId('pills-transfer-tab')).show();
            });
        });

        page.addEventListener('click', async function (event) {
            const button = event.target.closest('button[data-action]');
            if (!button || button.disabled || state.loading || state.busy || state.confirming || !state.loaded) return;
            const id = Number(button.dataset.id);
            const branch = findBranch(id);
            const room = state.phongHocs.find(item => item.phongHocId === id);
            switch (button.dataset.action) {
                case 'edit-branch':
                    if (branch) openBranch(branch);
                    break;
                case 'branch-rooms':
                    byId('roomBranchFilter').value = String(id);
                    renderRooms();
                    bootstrap.Tab.getOrCreateInstance(byId('pills-room-tab')).show();
                    break;
                case 'status-branch':
                    if (!branch) return;
                    if (await confirmAction(`${branch.trangThai ? 'Tạm ngưng' : 'Kích hoạt'} cơ sở “${branch.tenCoSo}”?`,
                        branch.trangThai ? 'Cơ sở tạm ngưng sẽ không được chọn làm nơi điều chuyển đến hoặc thêm phòng học mới.' : 'Cơ sở sẽ hoạt động trở lại.',
                        branch.trangThai ? 'Tạm ngưng' : 'Kích hoạt')) {
                        await mutate(page.dataset.statusBranchUrl, { coSoId: id, trangThai: !branch.trangThai }, null, 'pageMessage', 'Đã cập nhật trạng thái cơ sở.');
                    }
                    break;
                case 'edit-room':
                    if (room) openRoom(room);
                    break;
                case 'delete-room':
                    if (!room) return;
                    if (await confirmAction(`Xóa phòng “${room.tenPhong}”?`, 'Chỉ xóa được phòng không còn lịch học sắp tới hoặc đang diễn ra. Lịch sử được giữ lại.', 'Xóa Phòng Học')) {
                        await mutate(page.dataset.deleteRoomUrl, { phongHocId: id }, null, 'pageMessage', 'Đã xóa phòng học.');
                    }
                    break;
            }
        });

        // Keep a pending form open so the result cannot be lost behind another dialog.
        ['modalBranch', 'modalRoom', 'modalTransfer'].forEach(id => {
            byId(id).addEventListener('hide.bs.modal', event => {
                if (state.busy && !allowModalClose) event.preventDefault();
            });
            byId(id).addEventListener('shown.bs.modal', () => {
                const input = byId(id).querySelector('input:not([type="hidden"]), select');
                if (input) input.focus();
            });
        });

        loadData();
    }
})();
