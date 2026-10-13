import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement, parse_xml
from docx.oxml.ns import nsdecls, qn

FIGURES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "c4_figures")
os.makedirs(FIGURES_DIR, exist_ok=True)

# -------------------------------------------------------------
# 1. HÀM VẼ DIAGRAM CHUẨN VECTOR / HIGH-DPI
# -------------------------------------------------------------

def set_academic_font():
    plt.rcParams['font.family'] = 'serif'
    plt.rcParams['font.serif'] = ['Times New Roman', 'DejaVu Serif', 'Liberation Serif', 'serif']
    plt.rcParams['font.size'] = 9.5
    plt.rcParams['axes.edgecolor'] = '#64748B'
    plt.rcParams['axes.linewidth'] = 0.8

def draw_box(ax, x, y, w, h, title, subtitle, desc, bg_color='#F1F5F9', border_color='#334155', text_color='#0F172A', box_style='round,pad=0.2'):
    rect = patches.FancyBboxPatch(
        (x, y), w, h,
        boxstyle=box_style,
        facecolor=bg_color,
        edgecolor=border_color,
        linewidth=1.2,
        zorder=2
    )
    ax.add_patch(rect)
    
    # Text inside box
    cx = x + w / 2.0
    # Title
    ax.text(cx, y + h - 0.25, title, ha='center', va='top', fontsize=9.5, fontweight='bold', color=text_color, zorder=3)
    # Subtitle [Type / Tech]
    if subtitle:
        ax.text(cx, y + h - 0.50, f"[{subtitle}]", ha='center', va='top', fontsize=8.0, style='italic', color='#475569', zorder=3)
    # Description
    if desc:
        ax.text(cx, y + 0.15, desc, ha='center', va='bottom', fontsize=7.5, color='#334155', wrap=True, zorder=3)

def draw_arrow(ax, x1, y1, x2, y2, label='', label_pos=(0.5, 0.5), color='#475569', style='-', connectionstyle='arc3,rad=0.0'):
    ax.annotate(
        '', xy=(x2, y2), xytext=(x1, y1),
        arrowprops=dict(
            arrowstyle="-|>",
            color=color,
            lw=1.1,
            ls=style,
            connectionstyle=connectionstyle,
            shrinkA=4,
            shrinkB=4,
            mutation_scale=12
        ),
        zorder=1
    )
    if label:
        lx = x1 + (x2 - x1) * label_pos[0]
        ly = y1 + (y2 - y1) * label_pos[1]
        ax.text(
            lx, ly, label,
            ha='center', va='center',
            fontsize=7.2,
            color='#1E293B',
            bbox=dict(boxstyle='round,pad=0.2', facecolor='#FFFFFF', edgecolor='#CBD5E1', lw=0.6, alpha=0.95),
            zorder=4
        )

# --- SƠ ĐỒ 1: C4 CONTEXT ---
def generate_context_diagram():
    fig, ax = plt.subplots(figsize=(11.5, 8.0), dpi=300)
    ax.set_xlim(0, 11.5)
    ax.set_ylim(0, 8.0)
    ax.axis('off')
    set_academic_font()

    # Core System Boundary
    boundary = patches.FancyBboxPatch((3.5, 1.4), 4.4, 5.2, boxstyle='round,pad=0.25', facecolor='#F8FAFC', edgecolor='#64748B', lw=1.2, ls='--', zorder=0)
    ax.add_patch(boundary)
    ax.text(5.7, 6.35, "Hệ Thống Vận Chuyển Ngựa Đua Xuyên Quốc Gia", ha='center', va='bottom', fontsize=10.5, fontweight='bold', color='#1E3A8A')

    # Core System Box
    draw_box(ax, 3.8, 1.8, 3.8, 4.3,
             "Racehorse Transport System", "Software System - Cốt Lõi",
             "Quản lý toàn diện vòng đời vận chuyển ngựa đua:\n- Tiếp nhận và duyệt yêu cầu đặt đơn\n- Quản lý quy định kiểm dịch & thẩm định hồ sơ\n- Lập kế hoạch lộ trình, điều phối xe & máy bay\n- Giám sát vị trí GPS và sức khỏe ngựa thời gian thực\n- Bàn giao, kiểm tra thể trạng và ký số tại đích.",
             bg_color='#EFF6FF', border_color='#1D4ED8', text_color='#1E3A8A')

    # Actors (Left column)
    draw_box(ax, 0.4, 5.6, 2.5, 1.6, "Customer", "Person", "Chủ ngựa, quản lý trại đua tạo yêu cầu,\ntheo dõi vị trí và ký nhận bàn giao.", bg_color='#F1F5F9', border_color='#475569')
    draw_box(ax, 0.4, 3.3, 2.5, 1.6, "Logistics Manager", "Person", "Quản lý điều hành phê duyệt đơn,\nđiều phối lịch và xử lý phát sinh.", bg_color='#F1F5F9', border_color='#475569')
    draw_box(ax, 0.4, 1.0, 2.5, 1.6, "Compliance Specialist", "Person", "Chuyên viên thẩm định chứng từ thú y,\nhồ sơ hải quan và thủ tục thông quan.", bg_color='#F1F5F9', border_color='#475569')

    # External Systems (Right column)
    draw_box(ax, 8.5, 5.6, 2.5, 1.6, "Maps & Geocoding", "External System", "Google Maps / OpenStreetMap cung cấp\ndữ liệu tọa độ bản đồ, cự ly và tính ETA.", bg_color='#FEF3C7', border_color='#D97706')
    draw_box(ax, 8.5, 3.3, 2.5, 1.6, "Cloud Object Storage", "External System - S3/MinIO", "Lưu trữ tài liệu chứng từ, ảnh sức khỏe,\nbiên bản và chữ ký điện tử.", bg_color='#FEF3C7', border_color='#D97706')
    draw_box(ax, 8.5, 1.0, 2.5, 1.6, "SMS / Email Gateway", "External System", "Dịch vụ gửi thông báo tự động đa kênh\n(Twilio, SendGrid, Firebase FCM).", bg_color='#FEF3C7', border_color='#D97706')

    # Connections Left to Center
    draw_arrow(ax, 2.9, 6.4, 3.8, 5.4, "Tạo đơn, theo dõi\n[HTTPS / Web & App]", (0.45, 0.55))
    draw_arrow(ax, 2.9, 4.1, 3.8, 4.1, "Duyệt đơn, giám sát\n[HTTPS / Web Portal]", (0.45, 0.5))
    draw_arrow(ax, 2.9, 1.8, 3.8, 2.8, "Duyệt kiểm dịch\n[HTTPS / Web Portal]", (0.45, 0.45))

    # Connections Center to Right
    draw_arrow(ax, 7.6, 5.4, 8.5, 6.4, "Truy vấn tọa độ & ETA\n[REST / HTTPS]", (0.55, 0.55))
    draw_arrow(ax, 7.6, 4.1, 8.5, 4.1, "Lưu file chứng chỉ, ảnh\n[S3 API / HTTPS]", (0.55, 0.5))
    draw_arrow(ax, 7.6, 2.8, 8.5, 1.8, "Gửi email / SMS cảnh báo\n[REST / HTTPS]", (0.55, 0.45))

    plt.tight_layout()
    path = os.path.join(FIGURES_DIR, "fig1_c4_context.png")
    fig.savefig(path, bbox_inches='tight', dpi=300)
    plt.close(fig)
    return path

# --- SƠ ĐỒ 2: C4 CONTAINER ---
def generate_container_diagram():
    fig, ax = plt.subplots(figsize=(13.0, 10.5), dpi=300)
    ax.set_xlim(0, 13.0)
    ax.set_ylim(0, 10.5)
    ax.axis('off')
    set_academic_font()

    # System boundary
    boundary = patches.FancyBboxPatch((0.4, 0.4), 12.2, 8.4, boxstyle='round,pad=0.2', facecolor='#F8FAFC', edgecolor='#64748B', lw=1.2, ls='--', zorder=0)
    ax.add_patch(boundary)
    ax.text(0.7, 8.55, "Ranh Giới Nền Tảng Microservices (Racehorse Transport Platform)", ha='left', va='bottom', fontsize=10.5, fontweight='bold', color='#1E3A8A')

    # Clients (Top - Outside Boundary)
    draw_box(ax, 2.6, 9.2, 3.4, 0.95, "Mobile Application", "React Native / Expo", "Ứng dụng cho Customer & Driver", bg_color='#F1F5F9', border_color='#334155')
    draw_box(ax, 7.0, 9.2, 3.4, 0.95, "Web Management Portal", "React / ASP.NET Blazor", "Cổng thông tin cho Manager & Specialist", bg_color='#F1F5F9', border_color='#334155')

    # Gateway (Inside Boundary, Top)
    draw_box(ax, 4.3, 7.3, 4.4, 1.05, "API Gateway", "YARP Reverse Proxy / .NET 10", "Điểm đón tiếp duy nhất, định tuyến URL,\nxác thực JWT Bearer, cân bằng tải", bg_color='#DBEAFE', border_color='#1D4ED8')

    draw_arrow(ax, 4.3, 9.2, 5.5, 8.35, "JSON / HTTPS + JWT", (0.35, 0.5))
    draw_arrow(ax, 8.7, 9.2, 7.5, 8.35, "JSON / HTTPS + JWT", (0.65, 0.5))

    # Row 1: Core Domain Microservices
    draw_box(ax, 0.6, 5.5, 2.6, 1.25, "Identity Service", ".NET 10 Minimal API", "Quản lý User, Role,\nAccess & Refresh Token", bg_color='#E0F2FE', border_color='#0284C7')
    draw_box(ax, 3.7, 5.5, 2.6, 1.25, "Booking Service", ".NET 10 Minimal API", "Customer, Horse,\nTransportRequest, Order", bg_color='#E0F2FE', border_color='#0284C7')
    draw_box(ax, 6.7, 5.5, 2.6, 1.25, "Compliance Service", ".NET 10 Minimal API", "Kiểm dịch, chứng từ y tế,\nhải quan và Readiness", bg_color='#E0F2FE', border_color='#0284C7')
    draw_box(ax, 9.8, 5.5, 2.6, 1.25, "Planning Service", ".NET 10 Minimal API", "Lập kế hoạch Trip, Route,\nđiều phối xe & máy bay", bg_color='#E0F2FE', border_color='#0284C7')

    # Gateway Routing Arrows down to Row 1
    draw_arrow(ax, 4.8, 7.3, 1.9, 6.75, "/api/v1/auth", (0.3, 0.5))
    draw_arrow(ax, 5.8, 7.3, 5.0, 6.75, "/api/v1/booking", (0.45, 0.5))
    draw_arrow(ax, 7.2, 7.3, 8.0, 6.75, "/api/v1/compliance", (0.55, 0.5))
    draw_arrow(ax, 8.2, 7.3, 11.1, 6.75, "/api/v1/planning", (0.7, 0.5))

    # MIDDLE ROW: Shared Message Broker & Object Storage
    draw_box(ax, 1.2, 3.5, 5.0, 1.3, "RabbitMQ Event Bus", "Message Broker - Topic Exchange", "Hàng đợi thông điệp tích hợp qua topic racehorse.events\nĐảm bảo tính nhất quán dữ liệu qua Outbox Pattern", bg_color='#FFEDD5', border_color='#EA580C')
    draw_box(ax, 6.8, 3.5, 5.0, 1.3, "MinIO Object Storage", "S3 Compatible Object Store", "Lưu trữ tệp tin nhị phân lớn:\nFile PDF hộ chiếu, kết quả xét nghiệm, ảnh hiện trường sự cố", bg_color='#FEF08A', border_color='#CA8A04')

    # Publish events from Row 1 down to RabbitMQ / MinIO
    draw_arrow(ax, 5.0, 5.5, 3.7, 4.8, "Outbox: OrderCreated", (0.4, 0.5), color='#EA580C', style='--')
    draw_arrow(ax, 8.0, 5.5, 5.0, 4.8, "Outbox: ComplianceReady", (0.6, 0.4), color='#EA580C', style='--')
    draw_arrow(ax, 4.8, 4.8, 10.5, 5.5, "Consume: ComplianceReady", (0.7, 0.6), color='#EA580C', style='--')
    draw_arrow(ax, 8.3, 5.5, 8.8, 4.8, "Upload Docs (S3 API)", (0.5, 0.5), color='#CA8A04', style=':')

    # Row 2: Operational & Support Microservices
    draw_box(ax, 0.6, 1.2, 2.6, 1.25, "Tracking Service", ".NET 10 Minimal API", "Định vị GPS, Milestones,\nnhật ký sức khỏe ngựa", bg_color='#E0F2FE', border_color='#0284C7')
    draw_box(ax, 3.7, 1.2, 2.6, 1.25, "Incident Service", ".NET 10 Minimal API", "Quản lý sự cố, rủi ro,\nkích hoạt đổi tuyến khẩn", bg_color='#E0F2FE', border_color='#0284C7')
    draw_box(ax, 6.7, 1.2, 2.6, 1.25, "Handover Service", ".NET 10 Minimal API", "Biên bản bàn giao đích,\nkiểm tra ngựa, chữ ký số", bg_color='#E0F2FE', border_color='#0284C7')
    draw_box(ax, 9.8, 1.2, 2.6, 1.25, "Notification Service", ".NET 10 Minimal API", "Gửi Email, SMS, Webhook\nvà Push Notifications", bg_color='#E0F2FE', border_color='#0284C7')

    # Events between Middle Row and Row 2
    draw_arrow(ax, 2.5, 2.45, 2.5, 3.5, "TripDeparted", (0.5, 0.5), color='#EA580C', style='--')
    draw_arrow(ax, 5.0, 2.45, 4.5, 3.5, "IncidentCreated", (0.5, 0.5), color='#EA580C', style='--')
    draw_arrow(ax, 8.0, 2.45, 5.5, 3.5, "HandoverCompleted", (0.5, 0.5), color='#EA580C', style='--')
    draw_arrow(ax, 6.2, 3.5, 10.5, 2.45, "Subscribe Events", (0.65, 0.5), color='#EA580C', style='--')

    plt.tight_layout()
    path = os.path.join(FIGURES_DIR, "fig2_c4_container.png")
    fig.savefig(path, bbox_inches='tight', dpi=300)
    plt.close(fig)
    return path

# --- SƠ ĐỒ 3: C4 COMPONENT (COMPLIANCE SERVICE) ---
def generate_component_diagram():
    fig, ax = plt.subplots(figsize=(11.5, 8.5), dpi=300)
    ax.set_xlim(0, 11.5)
    ax.set_ylim(0, 8.5)
    ax.axis('off')
    set_academic_font()

    # Container boundary
    boundary = patches.FancyBboxPatch((0.5, 0.5), 10.5, 7.4, boxstyle='round,pad=0.2', facecolor='#F8FAFC', edgecolor='#0284C7', lw=1.2, ls='--', zorder=0)
    ax.add_patch(boundary)
    ax.text(5.75, 8.0, "Thành Phần Nội Tại Compliance Service (BE2 Scope)", ha='center', va='bottom', fontsize=10, fontweight='bold', color='#0369A1')

    # API Endpoints
    draw_box(ax, 4.0, 6.7, 3.5, 0.9, "Compliance Minimal APIs", "API Endpoints Layer", "Endpoints: /document-types, /regulations,\n/documents, /clearance, /readiness/evaluate", bg_color='#E0F2FE', border_color='#0284C7')

    # Application Layer (4 Core Services)
    draw_box(ax, 0.8, 4.6, 2.3, 1.3, "MasterData Service", "Application Service", "Quản lý danh mục loại hồ sơ\nvà quy tắc kiểm dịch các quốc gia.", bg_color='#F0FDF4', border_color='#16A34A')
    draw_box(ax, 3.4, 4.6, 2.3, 1.3, "Document Service", "Application Service", "Xử lý tải lên, lưu trữ metadata\nvà thẩm định phê duyệt chứng từ.", bg_color='#F0FDF4', border_color='#16A34A')
    draw_box(ax, 6.0, 4.6, 2.3, 1.3, "Clearance Service", "Application Service", "Tạo, nộp hồ sơ hải quan\nvà phê duyệt thông quan cửa khẩu.", bg_color='#F0FDF4', border_color='#16A34A')
    draw_box(ax, 8.6, 4.6, 2.3, 1.3, "Readiness Service", "Evaluation Engine", "Bộ máy kiểm tra tính hợp lệ toàn diện\nvà kích hoạt ComplianceReady.", bg_color='#F0FDF4', border_color='#16A34A')

    # Calls from API to Application Services
    draw_arrow(ax, 4.6, 6.7, 2.0, 5.9, "Invoke", (0.3, 0.5))
    draw_arrow(ax, 5.2, 6.7, 4.5, 5.9, "Invoke", (0.45, 0.5))
    draw_arrow(ax, 6.3, 6.7, 7.1, 5.9, "Invoke", (0.55, 0.5))
    draw_arrow(ax, 6.9, 6.7, 9.7, 5.9, "Invoke", (0.7, 0.5))

    # Persistence & Infrastructure Layer
    draw_box(ax, 1.8, 2.4, 4.0, 1.2, "ComplianceDbContext", "EF Core 10 / Infrastructure", "Unit of Work quản lý 9 bảng thực thể\nvà bảng OutboxMessages (Dual-write safe)", bg_color='#FEF3C7', border_color='#D97706')
    draw_box(ax, 6.4, 2.4, 3.8, 1.2, "File Storage Service", "Local / S3 MinIO Adapter", "Xử lý lưu file nhị phân (PDF, ảnh y tế)\nvào Local File System hoặc S3 Bucket", bg_color='#FEF3C7', border_color='#D97706')

    # Background Outbox Dispatcher
    draw_box(ax, 1.8, 0.8, 4.0, 1.0, "Outbox Dispatcher", "BackgroundService / Worker", "Quét bảng Outbox định kỳ (5 giây),\nphát sự kiện sang RabbitMQ & mark PROCESSED", bg_color='#FFEDD5', border_color='#EA580C')

    # External targets
    draw_box(ax, 7.0, 0.8, 3.2, 1.0, "RabbitMQ Topic Exchange", "External Broker: racehorse.events", "Nhận sự kiện Compliance.ComplianceReady", bg_color='#FEE2E2', border_color='#DC2626')

    # Connections to DB & File
    draw_arrow(ax, 2.0, 4.6, 3.2, 3.6, "CRUD Rules", (0.3, 0.5))
    draw_arrow(ax, 4.5, 4.6, 3.8, 3.6, "Save Document", (0.5, 0.5))
    draw_arrow(ax, 7.1, 4.6, 4.8, 3.6, "Save Clearance", (0.5, 0.5))
    draw_arrow(ax, 9.7, 4.6, 5.4, 3.6, "Query & Insert Outbox", (0.7, 0.5))

    draw_arrow(ax, 4.6, 4.6, 7.8, 3.6, "Save Stream", (0.6, 0.5))
    draw_arrow(ax, 3.8, 2.4, 3.8, 1.8, "Poll Outbox", (0.5, 0.5))
    draw_arrow(ax, 5.8, 1.3, 7.0, 1.3, "Publish AMQP", (0.5, 0.5))

    plt.tight_layout()
    path = os.path.join(FIGURES_DIR, "fig3_c4_component.png")
    fig.savefig(path, bbox_inches='tight', dpi=300)
    plt.close(fig)
    return path

# --- SƠ ĐỒ 4: DYNAMIC FLOW DIAGRAM ---
def generate_dynamic_flow_diagram():
    fig, ax = plt.subplots(figsize=(11.5, 7.0), dpi=300)
    ax.set_xlim(0, 11.5)
    ax.set_ylim(0, 7.0)
    ax.axis('off')
    set_academic_font()

    # Steps in horizontal/vertical flow
    steps = [
        ("1. Tạo & Duyệt Yêu Cầu", "Customer tạo yêu cầu vận chuyển.\nLogistics Manager phê duyệt đơn.\nBooking sinh sự kiện: OrderCreated.", 0.6, 4.2),
        ("2. Kiểm Dịch & Pháp Lý", "Khách hàng upload chứng từ thú y.\nCompliance Specialist thẩm định hồ sơ.\nCompliance sinh: ComplianceReady.", 4.5, 4.2),
        ("3. Lập Lịch & Phân Bổ", "Planning Service nhận ComplianceReady.\nPhân bổ phương tiện, chuồng, tài xế.\nPlanning kích hoạt: TripReady.", 8.4, 4.2),
        ("4. Thực Thi & Giám Sát", "Tài xế nhận lệnh, bắt đầu chuyến đi.\nTracking cập nhật GPS và sức khỏe ngựa.\nTracking sinh: CheckpointArrived.", 8.4, 1.2),
        ("5. Bàn Giao & Nghiệm Thu", "Đoàn xe đến đích an toàn.\nBác sĩ thú y kiểm tra thể trạng ngựa.\nKhách hàng ký điện tử: HandoverCompleted.", 4.5, 1.2),
        ("6. Quyết Toán & Giải Phóng", "Handover chốt doanh thu và chi phí.\nBooking hoàn tất đơn hàng.\nPlanning giải phóng xe vào Pool.", 0.6, 1.2),
    ]

    for title, desc, x, y in steps:
        draw_box(ax, x, y, 2.7, 1.8, title, "Workflow Phase", desc, bg_color='#F8FAFC', border_color='#1E3A8A')

    # Transitions
    draw_arrow(ax, 3.3, 5.1, 4.5, 5.1, "OrderCreated", (0.5, 0.6))
    draw_arrow(ax, 7.2, 5.1, 8.4, 5.1, "ComplianceReady", (0.5, 0.6))
    draw_arrow(ax, 9.75, 4.2, 9.75, 3.0, "TripReady", (0.7, 0.5))
    draw_arrow(ax, 8.4, 2.1, 7.2, 2.1, "Arrived at Destination", (0.5, 0.6))
    draw_arrow(ax, 4.5, 2.1, 3.3, 2.1, "HandoverCompleted", (0.5, 0.6))

    plt.tight_layout()
    path = os.path.join(FIGURES_DIR, "fig4_dynamic_flow.png")
    fig.savefig(path, bbox_inches='tight', dpi=300)
    plt.close(fig)
    return path

# -------------------------------------------------------------
# 2. XÂY DỰNG FILE WORD CHUẨN ACADEMIC
# -------------------------------------------------------------

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def style_academic_table(table):
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    # Set borders: top, bottom, and header bottom (Booktabs academic style)
    tblPr = table._tbl.tblPr
    borders = parse_xml(
        f'<w:tblBorders {nsdecls("w")}>\n'
        f'  <w:top w:val="single" w:sz="8" w:space="0" w:color="333333"/>\n'
        f'  <w:bottom w:val="single" w:sz="8" w:space="0" w:color="333333"/>\n'
        f'  <w:insideH w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/>\n'
        f'  <w:left w:val="none"/>\n'
        f'  <w:right w:val="none"/>\n'
        f'  <w:insideV w:val="none"/>\n'
        f'</w:tblBorders>'
    )
    tblPr.append(borders)

def build_docx_report(img_paths, output_docx_path):
    doc = Document()

    # 1. Page Margins (TCVN Academic Standards: Left 3cm, Right 2cm, Top 2cm, Bottom 2cm)
    for section in doc.sections:
        section.top_margin = Inches(0.79)     # 2.0 cm
        section.bottom_margin = Inches(0.79)  # 2.0 cm
        section.left_margin = Inches(1.18)    # 3.0 cm
        section.right_margin = Inches(0.79)   # 2.0 cm

    # 2. Base Typography: Times New Roman throughout
    style_normal = doc.styles['Normal']
    font = style_normal.font
    font.name = 'Times New Roman'
    font.size = Pt(12)
    font.color.rgb = RGBColor(0, 0, 0)
    style_normal.paragraph_format.line_spacing = 1.3
    style_normal.paragraph_format.space_after = Pt(6)
    style_normal.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY

    # Helper function for adding paragraphs
    def add_p(text, bold=False, italic=False, align=WD_ALIGN_PARAGRAPH.JUSTIFY, space_after=6, space_before=0):
        p = doc.add_paragraph()
        p.alignment = align
        p.paragraph_format.space_after = Pt(space_after)
        p.paragraph_format.space_before = Pt(space_before)
        p.paragraph_format.line_spacing = 1.3
        run = p.add_run(text)
        run.bold = bold
        run.italic = italic
        run.font.name = 'Times New Roman'
        run.font.size = Pt(12)
        return p

    def add_heading_1(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(14)
        p.paragraph_format.space_after = Pt(6)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.bold = True
        run.font.name = 'Times New Roman'
        run.font.size = Pt(14)
        run.font.color.rgb = RGBColor(0, 0, 0)
        return p

    def add_heading_2(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(10)
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.bold = True
        run.font.name = 'Times New Roman'
        run.font.size = Pt(13)
        run.font.color.rgb = RGBColor(0, 0, 0)
        return p

    def add_heading_3(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(6)
        p.paragraph_format.space_after = Pt(2)
        p.paragraph_format.keep_with_next = True
        run = p.add_run(text)
        run.bold = True
        run.font.name = 'Times New Roman'
        run.font.size = Pt(12)
        run.font.color.rgb = RGBColor(0, 0, 0)
        return p

    def add_figure(img_path, caption):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(6)
        p.paragraph_format.space_after = Pt(4)
        p.paragraph_format.keep_with_next = True
        run = p.add_run()
        run.add_picture(img_path, width=Inches(6.2))

        p_cap = doc.add_paragraph()
        p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p_cap.paragraph_format.space_before = Pt(2)
        p_cap.paragraph_format.space_after = Pt(10)
        run_cap = p_cap.add_run(caption)
        run_cap.italic = True
        run_cap.font.name = 'Times New Roman'
        run_cap.font.size = Pt(10.5)

    # ------------------ COVER / HEADER ------------------
    add_p("TRƯỜNG ĐẠI HỌC FPT", bold=True, align=WD_ALIGN_PARAGRAPH.CENTER, space_after=2)
    add_p("KHOA CÔNG NGHỆ THÔNG TIN - BỘ MÔN C# / .NET", align=WD_ALIGN_PARAGRAPH.CENTER, space_after=18)
    
    add_p("BÁO CÁO THIẾT KẾ KIẾN TRÚC HỆ THỐNG", bold=True, align=WD_ALIGN_PARAGRAPH.CENTER, space_after=6)
    add_p("HỆ THỐNG QUẢN LÝ VẬN CHUYỂN NGỰA ĐUA XUYÊN QUỐC GIA", bold=True, align=WD_ALIGN_PARAGRAPH.CENTER, space_after=4)
    add_p("MÔ HÌNH HÓA KIẾN TRÚC PHẦN MỀM THEO C4 MODEL", align=WD_ALIGN_PARAGRAPH.CENTER, space_after=24)

    # Metadata table
    meta_table = doc.add_table(rows=4, cols=2)
    meta_table.alignment = WD_TABLE_ALIGNMENT.CENTER
    meta_data = [
        ("Môn học:", "PRN232 - Advanced Cross-Platform Application Development"),
        ("Đề tài nghiên cứu:", "Cross-Border Racehorse Transport Management System"),
        ("Vai trò kỹ thuật:", "Backend Engineer 2 (BE2) - Compliance & Core Integration"),
        ("Khung kiến trúc áp dụng:", "C4 Model (Simon Brown) & Event-Driven Microservices Architecture")
    ]
    for i, (k, v) in enumerate(meta_data):
        row = meta_table.rows[i]
        c0, c1 = row.cells[0], row.cells[1]
        c0.width = Inches(2.2)
        c1.width = Inches(4.2)
        c0.paragraphs[0].text = k
        c0.paragraphs[0].runs[0].bold = True
        c0.paragraphs[0].runs[0].font.name = 'Times New Roman'
        c1.paragraphs[0].text = v
        c1.paragraphs[0].runs[0].font.name = 'Times New Roman'
        set_cell_margins(c0, 40, 40, 80, 80)
        set_cell_margins(c1, 40, 40, 80, 80)
    style_academic_table(meta_table)

    add_p("", space_after=18)

    # ------------------ CHƯƠNG 1 ------------------
    add_heading_1("1. Tổng Quan Bài Toán Và Mục Tiêu Kiến Trúc")
    add_p("Hệ thống quản lý vận chuyển ngựa đua xuyên quốc gia (Cross-Border Racehorse Transport Management System) được xây dựng nhằm giải quyết bài toán logistics chuyên biệt có độ phức tạp cao. Ngựa đua là đối tượng vận chuyển có giá trị kinh tế đặc biệt lớn, đòi hỏi quy trình giám sát nghiêm ngặt về sức khỏe sinh học, bảo đảm điều kiện chuồng trại, thời gian di chuyển và tuân thủ tuyệt đối các quy định kiểm dịch quốc tế giữa các quốc gia xuất xứ, quá cảnh và đích đến.")
    add_p("Về mặt kỹ thuật, hệ thống phải đáp ứng các yêu cầu phi chức năng cốt lõi bao gồm tính khả dụng cao, khả năng mở rộng độc lập giữa các phân hệ nghiệp vụ, tính toàn vẹn giao dịch và bảo mật dữ liệu. Để đạt được các mục tiêu này, giải pháp kiến trúc được lựa chọn là mô hình Microservices hướng sự kiện (Event-Driven Architecture) trên nền tảng .NET 10 LTS, kết hợp cổng kết nối tập trung YARP API Gateway, hàng đợi thông điệp RabbitMQ và cơ chế lưu trữ phân tán cơ sở dữ liệu riêng biệt cho từng dịch vụ (Database-per-Service).")

    # ------------------ CHƯƠNG 2 ------------------
    add_heading_1("2. Cơ Sở Phương Pháp Luận C4 Model")
    add_p("C4 Model là phương pháp luận mô hình hóa kiến trúc phần mềm do Simon Brown khởi xướng, cung cấp góc nhìn phân tầng trực quan theo bốn mức độ trừu tượng tương tự như bản đồ địa lý:")
    add_p("1. Mức độ 1 - Ngữ cảnh hệ thống (System Context): Xác định hệ thống phần mềm trong mối tương quan với người dùng thực tế và các hệ sinh thái bên ngoài.")
    add_p("2. Mức độ 2 - Phân rã Container (Container Diagram): Mô tả các đơn vị có thể triển khai và thực thi độc lập (các ứng dụng web, ứng dụng di động, các dịch vụ API, cơ sở dữ liệu và hệ thống hàng đợi).")
    add_p("3. Mức độ 3 - Thành phần nội tại (Component Diagram): Phân rã sâu vào bên trong một Container trọng yếu nhằm làm rõ các module nghiệp vụ, controller, service và tầng giao tiếp dữ liệu.")
    add_p("4. Mức độ 4 - Mã nguồn (Code Diagram): Mô tả chi tiết mức lớp (Class/Interface) và các mẫu thiết kế cụ thể trong mã nguồn.")
    add_p("Theo nguyên lý chuẩn của Simon Brown, tài liệu thiết kế kiến trúc chuẩn mực cho phần lớn các hệ thống thương mại tập trung chủ yếu vào Cấp độ 1 (Context) và Cấp độ 2 (Container), bổ sung Cấp độ 3 cho các dịch vụ phức tạp mang tính then chốt.")

    # ------------------ CHƯƠNG 3 ------------------
    add_heading_1("3. Cấp Độ 1: Biểu Đồ Ngữ Cảnh Hệ Thống (System Context)")
    add_p("Sơ đồ ngữ cảnh hệ thống định vị phần mềm Racehorse Transport System ở trung tâm, xác định rõ các tác nhân vận hành chính và các hệ thống tích hợp ngoại vi.")
    
    add_figure(img_paths["context"], "Hình 3.1: Sơ đồ ngữ cảnh hệ thống (C4 Level 1 - System Context Diagram)")

    add_heading_2("3.1. Phân Tích Các Tác Nhân Hệ Thống (Actors)")
    add_p("Hệ thống phục vụ sáu nhóm tác nhân với vai trò và quyền hạn phân định rõ:")
    
    actors_table = doc.add_table(rows=7, cols=3)
    actors_data = [
        ("Tác nhân (Actor)", "Mô tả vai trò", "Kênh tương tác chính"),
        ("Customer", "Chủ ngựa hoặc đại diện trang trại tạo yêu cầu vận chuyển, tải lên hồ sơ hộ chiếu ngựa, theo dõi lộ trình và ký nhận bàn giao.", "Mobile Application & Web Portal"),
        ("Logistics Manager", "Quản lý điều hành duyệt đơn vận chuyển, phân bổ chuyến, giám sát tiến độ toàn mạng lưới và xử lý sự cố bất thường.", "Web Operations Portal"),
        ("Compliance Specialist", "Chuyên viên thẩm định hồ sơ y tế, xác thực giấy phép kiểm dịch quốc tế và thụ lý thủ tục thông quan hải quan.", "Web Operations Portal"),
        ("Fleet Coordinator", "Điều phối viên quản lý đội xe chuyên dụng, lịch bay, chuồng cách ly và phân công nhân sự áp tải.", "Web Operations Portal"),
        ("Driver / Escort", "Tài xế và nhân viên áp tải cập nhật tọa độ GPS, ghi nhận mốc hành trình và theo dõi chỉ số sức khỏe sinh học của ngựa.", "Mobile Application"),
        ("System Admin", "Quản trị viên hệ thống quản lý tài khoản, phân quyền danh mục vai trò và cấu hình hạ tầng vận hành.", "Web Operations Portal")
    ]
    for i, row_data in enumerate(actors_data):
        row = actors_table.rows[i]
        for j, val in enumerate(row_data):
            c = row.cells[j]
            c.paragraphs[0].text = val
            if i == 0:
                c.paragraphs[0].runs[0].bold = True
            c.paragraphs[0].runs[0].font.name = 'Times New Roman'
            c.paragraphs[0].runs[0].font.size = Pt(10.5)
            set_cell_margins(c, 50, 50, 80, 80)
    style_academic_table(actors_table)

    add_heading_2("3.2. Hệ Thống Ngoại Vi Tích Hợp (External Systems)")
    add_p("Hệ thống kết nối trực tiếp với bốn hệ thống bên ngoài để hoàn thành chuỗi nghiệp vụ:")
    add_p("- Dịch vụ bản đồ và định vị (Google Maps / OpenStreetMap): Cung cấp dữ liệu địa lý, tối ưu hóa cung đường di chuyển, tính toán cự ly thực tế và ước tính thời gian đến nơi (ETA).")
    add_p("- Cổng lưu trữ đối tượng đám mây (Cloud Object Storage - MinIO / AWS S3): Lưu trữ an toàn các tệp nhị phân kích thước lớn như ảnh chụp hộ chiếu ngựa, giấy kiểm dịch song phương, ảnh hiện trường sự cố và chữ ký bàn giao.")
    add_p("- Cổng truyền thông (SMS / Email Gateway - Twilio / SendGrid): Phát thông báo tức thời tới khách hàng và nhân viên khi trạng thái chuyến đi thay đổi hoặc có cảnh báo y tế khẩn cấp.")
    add_p("- Cổng thông tin hải quan và kiểm dịch quốc gia: Điểm kết nối trao đổi dữ liệu pháp lý và xác thực mã số thông quan tại các cửa khẩu biên giới và sân bay quốc tế.")

    # ------------------ CHƯƠNG 4 ------------------
    add_heading_1("4. Cấp Độ 2: Biểu Đồ Phân Rã Container (Container Architecture)")
    add_p("Biểu đồ phân rã Container thể hiện kiến trúc vật lý và các ranh giới triển khai độc lập của hệ thống. Nền tảng được cấu trúc theo mô hình Microservices với cổng API Gateway duy nhất và 8 dịch vụ nghiệp vụ chuyên trách.")

    add_figure(img_paths["container"], "Hình 4.1: Sơ đồ phân rã Container (C4 Level 2 - Container Diagram)")

    add_heading_2("4.1. Phân Tầng Ứng Dụng Và Điểm Truy Cập Tập Trung")
    add_p("Tầng giao diện người dùng được phân tách thành ứng dụng di động đa nền tảng (React Native / Expo) phục vụ tác vụ di động ngoài hiện trường và ứng dụng web quản trị (Web Portal) phục vụ tác vụ văn phòng điều hành.")
    add_p("API Gateway được xây dựng dựa trên công nghệ YARP (Yet Another Reverse Proxy) của Microsoft trên .NET 10. API Gateway đóng vai trò là điểm đón tiếp duy nhất cho toàn bộ lưu lượng truy cập từ bên ngoài, chịu trách nhiệm xác thực chữ ký số của mã thông báo JWT, thực thi chính sách phân quyền sơ bộ, định tuyến đường dẫn theo tiền tố nghiệp vụ và che giấu toàn bộ địa chỉ mạng nội bộ của các vi dịch vụ.")

    add_heading_2("4.2. Danh Mục Các Vi Dịch Vụ Nghiệp Vụ (Microservices)")
    add_p("Hệ thống tuân thủ nghiêm ngặt nguyên tắc ranh giới ngữ cảnh (Bounded Context) theo thiết kế hướng miền (Domain-Driven Design):")

    services_table = doc.add_table(rows=9, cols=3)
    services_data = [
        ("Tên dịch vụ", "Trách nhiệm nghiệp vụ chính", "Cơ sở dữ liệu độc lập"),
        ("Identity Service", "Quản lý danh tính người dùng, phân quyền RBAC và cấp phát JWT token.", "IdentityDb"),
        ("Booking Service", "Quản lý hồ sơ khách hàng, ngựa đua, yêu cầu vận chuyển và đơn hàng chính thức.", "BookingDb"),
        ("Compliance Service", "Quản lý quy định kiểm dịch, thẩm định chứng từ y tế và đánh giá điều kiện sẵn sàng.", "ComplianceDb"),
        ("Planning Service", "Lập kế hoạch lộ trình theo phiên bản, điều phối xe, chuồng chuyên dụng và chuyến bay.", "PlanningDb"),
        ("Tracking Service", "Thu thập dữ liệu định vị GPS, ghi nhận mốc hành trình và nhật ký thân nhiệt ngựa.", "TrackingDb"),
        ("Incident Service", "Tiếp nhận báo cáo sự cố trên đường, đánh giá rủi ro và kích hoạt đổi tuyến khẩn cấp.", "IncidentDb"),
        ("Handover Service", "Thực hiện biên bản bàn giao tại đích, giám định sức khỏe cuối chuyến và ký điện tử.", "HandoverDb"),
        ("Notification Service", "Lắng nghe sự kiện toàn hệ thống và phát thông báo đa kênh tới người dùng.", "NotificationDb")
    ]
    for i, row_data in enumerate(services_data):
        row = services_table.rows[i]
        for j, val in enumerate(row_data):
            c = row.cells[j]
            c.paragraphs[0].text = val
            if i == 0:
                c.paragraphs[0].runs[0].bold = True
            c.paragraphs[0].runs[0].font.name = 'Times New Roman'
            c.paragraphs[0].runs[0].font.size = Pt(10.5)
            set_cell_margins(c, 50, 50, 80, 80)
    style_academic_table(services_table)

    add_heading_2("4.3. Nguyên Tắc Chủ Quyền Dữ Liệu (Database-per-Service) Và Giao Tiếp Bất Đồng Bộ")
    add_p("Mỗi dịch vụ sở hữu hoàn toàn schema và cơ sở dữ liệu riêng, tuyệt đối không thực hiện liên kết bảng (SQL Join) trực tiếp qua biên giới dịch vụ. Mọi nhu cầu truy vấn chéo được thực hiện thông qua API hoặc đồng bộ dữ liệu phi tập trung.")
    add_p("Hệ thống hàng đợi RabbitMQ đóng vai trò xương sống cho việc trao đổi thông tin bất đồng bộ theo cơ chế Publish/Subscribe qua Topic Exchange 'racehorse.events'. Nhằm giải quyết triệt để vấn đề mất mát thông điệp khi xảy ra sự cố mạng (Dual-write problem), toàn bộ dịch vụ đều áp dụng mẫu thiết kế Outbox Pattern: sự kiện nghiệp vụ được ghi cùng giao dịch với dữ liệu thực thể vào bảng OutboxMessages, sau đó một tác vụ chạy ngầm (Outbox Dispatcher) sẽ quét và phát tán sự kiện lên RabbitMQ.")

    # ------------------ CHƯƠNG 5 ------------------
    add_heading_1("5. Cấp Độ 3: Biểu Đồ Thành Phần Dịch Vụ Tuân Thủ Pháp Lý (Compliance Service)")
    add_p("Phân hệ Compliance Service (thuộc phạm vi kỹ thuật của Kỹ sư Backend 2 - BE2) giữ vai trò then chốt trong việc kiểm soát tính hợp pháp và an toàn dịch tễ trước khi một chuyến vận chuyển được phép khởi hành.")

    add_figure(img_paths["component"], "Hình 5.1: Sơ đồ thành phần dịch vụ Compliance Service (C4 Level 3 - Component Diagram)")

    add_heading_2("5.1. Cấu Trúc Các Tầng Thành Phần")
    add_p("Compliance Service được xây dựng theo kiến trúc phân tầng sạch (Clean Architecture):")
    add_p("1. Tầng Giao tiếp (Presentation Layer): Sử dụng ASP.NET Core Minimal APIs định nghĩa các nhóm endpoint: /document-types, /regulations, /documents, /clearance và /readiness/evaluate. Tầng này kết hợp bộ lọc xác thực JWT Bearer để trích xuất danh tính và vai trò người dùng.")
    add_p("2. Tầng Nghiệp vụ ứng dụng (Application Layer): Bao gồm bốn dịch vụ cốt lõi:")
    add_p("   - ComplianceMasterDataService: Quản trị danh mục loại chứng chỉ và bộ quy định kiểm dịch song phương giữa các quốc gia.")
    add_p("   - DocumentService: Tiếp nhận luồng tải tệp, thẩm định nội dung chứng từ thú y và cập nhật trạng thái phê duyệt.")
    add_p("   - ClearanceService: Thụ lý hồ sơ thông quan tại cửa khẩu, điều phối trạng thái xét duyệt và tạo sự kiện hải quan hoàn tất.")
    add_p("   - ComplianceReadinessService: Động cơ thẩm tra tính hợp lệ toàn diện của chuyến đi dựa trên bộ quy tắc hiện hành, tự động kích hoạt sự kiện Compliance.ComplianceReady khi 100% tiêu chí được thỏa mãn.")
    add_p("3. Tầng Hạ tầng và Lưu trữ (Infrastructure Layer):")
    add_p("   - ComplianceDbContext: Đơn vị làm việc (Unit of Work) ánh xạ ORM Entity Framework Core cho 9 bảng thực thể dữ liệu.")
    add_p("   - LocalFileStorageService: Bộ điều hợp lưu trữ tệp tin nhị phân lên ổ đĩa nội bộ hoặc MinIO S3.")
    add_p("   - ComplianceOutboxDispatcher: Tác vụ ngầm quét các bản ghi sự kiện chưa gửi và phát tán an toàn sang RabbitMQ qua giao thức AMQP.")

    # ------------------ CHƯƠNG 6 ------------------
    add_heading_1("6. Quy Trình Nghiệp Vụ Động Xuyên Suốt Hệ Thống")
    add_p("Sơ đồ luồng động mô tả sự phối hợp tuần tự và bất đồng bộ giữa sáu dịch vụ nghiệp vụ chính trong suốt vòng đời của một chuyến vận chuyển ngựa đua:")

    add_figure(img_paths["dynamic"], "Hình 6.1: Quy trình phối hợp nghiệp vụ liên dịch vụ (C4 Dynamic Workflow)")

    add_p("Quy trình nghiệp vụ trải qua sáu giai đoạn liên kết chặt chẽ:")
    add_p("Giai đoạn 1 (Tạo và duyệt yêu cầu): Khách hàng lập phiếu yêu cầu vận chuyển trên ứng dụng. Logistics Manager rà soát tính khả thi và phê duyệt đơn. Booking Service tạo đơn hàng chính thức và phát sinh sự kiện Booking.OrderCreated.")
    add_p("Giai đoạn 2 (Kiểm dịch và pháp lý): Nhận được sự kiện đơn hàng, Planning Service tạo bản nháp chuyến đi. Đồng thời, khách hàng tải lên các chứng chỉ y tế của ngựa. Chuyên viên Compliance Service rà soát và phê duyệt. Khi toàn bộ hồ sơ đạt chuẩn, hệ thống tự động phát tán sự kiện Compliance.ComplianceReady.")
    add_p("Giai đoạn 3 (Lập lịch và phân bổ nguồn lực): Planning Service tiếp nhận sự kiện sẵn sàng tuân thủ, kích hoạt thuật toán xếp lịch, gán biển số xe, mã số chuồng chuyên dụng và tài xế, sau đó công bố sự kiện Planning.TripReady.")
    add_p("Giai đoạn 4 (Thực thi và giám sát thời gian thực): Đoàn xe khởi hành. Tài xế sử dụng ứng dụng di động để check-in tại các mốc checkpoint. Dịch vụ Tracking liên tục ghi nhận tọa độ GPS và thông số sức khỏe sinh học của ngựa, phát cảnh báo nếu có dấu hiệu bất thường.")
    add_p("Giai đoạn 5 (Bàn giao và nghiệm thu tại đích): Xe đến điểm đích an toàn. Bác sĩ thú y độc lập kiểm tra thể trạng ngựa. Khách hàng thực hiện ký nhận số trên biên bản bàn giao, kích hoạt sự kiện Handover.HandoverCompleted.")
    add_p("Giai đoạn 6 (Quyết toán và giải phóng tài nguyên): Handover Service đóng biên bản và tính toán tổng chi phí phát sinh. Booking Service chuyển trạng thái đơn hàng sang hoàn thành. Planning Service tự động giải phóng phương tiện và nhân sự trở lại nhóm sẵn sàng nhận nhiệm vụ mới.")

    # ------------------ CHƯƠNG 7 ------------------
    add_heading_1("7. Mô Hình Triển Khai Hạ Tầng (Deployment Architecture)")
    add_p("Hệ thống được đóng gói hoàn toàn dưới dạng các container Docker và điều phối thống nhất thông qua tệp cấu hình docker-compose.yml trong môi trường phát triển và kiểm thử.")
    add_p("Tất cả các dịch vụ ứng dụng và thành phần trung gian được kết nối trong mạng ảo cô lập 'racehorse_net'. Cổng API Gateway lắng nghe tại cổng ngoại vi 8080 và định tuyến tới các container nội bộ:")
    add_p("- Cổng dịch vụ danh tính Identity API: Cổng nội bộ 8080 (Mapped cổng host: 8081).")
    add_p("- Cổng dịch vụ đặt đơn Booking API: Cổng nội bộ 8080 (Mapped cổng host: 8082).")
    add_p("- Cổng dịch vụ tuân thủ pháp lý Compliance API: Cổng nội bộ 8080 (Mapped cổng host: 8083).")
    add_p("- Cổng dịch vụ lập lịch Planning API: Cổng nội bộ 8080 (Mapped cổng host: 8084).")
    add_p("- Cổng dịch vụ giám sát Tracking API: Cổng nội bộ 8080 (Mapped cổng host: 8085).")
    add_p("- Cổng dịch vụ sự cố Incident API: Cổng nội bộ 8080 (Mapped cổng host: 8086).")
    add_p("- Cổng dịch vụ bàn giao Handover API: Cổng nội bộ 8080 (Mapped cổng host: 8087).")
    add_p("- Cổng dịch vụ thông báo Notification API: Cổng nội bộ 8080 (Mapped cổng host: 8088).")
    add_p("Hạ tầng lưu trữ và thông điệp bao gồm máy chủ cơ sở dữ liệu Microsoft SQL Server 2022 (cổng 1433), máy chủ thông điệp RabbitMQ 3.13 Management (cổng 5672 và cổng quản trị web 15672) cùng máy chủ lưu trữ đối tượng MinIO S3 (cổng API 9000 và bảng điều khiển 9001).")

    # ------------------ CHƯƠNG 8 ------------------
    add_heading_1("8. Kết Luận Và Đánh Giá Kiến Trúc")
    add_p("Thiết kế kiến trúc hệ thống vận chuyển ngựa đua xuyên quốc gia dựa trên khung phương pháp luận C4 Model đã cung cấp bức tranh toàn diện, mạch lạc và có cơ sở lý thuyết vững chắc từ góc nhìn tổng quan của doanh nghiệp đến chi tiết kỹ thuật triển khai.")
    add_p("Việc áp dụng mô hình Microservices hướng sự kiện kết hợp mẫu hình Outbox Pattern và chủ quyền dữ liệu phân tán không chỉ loại bỏ triệt để nguy cơ thắt nút cổ chai hiệu năng mà còn tạo tiền đề vững chắc cho việc bảo trì, phát triển độc lập giữa các nhóm kỹ sư và bảo đảm tính toàn vẹn nghiệp vụ xuyên suốt vòng đời vận hành.")

    doc.save(output_docx_path)
    print(f"Document successfully created at: {output_docx_path}")

# -------------------------------------------------------------
# 3. CHUYỂN ĐỔI SANG PDF QUA MS WORD COM
# -------------------------------------------------------------
def convert_to_pdf(docx_path, pdf_path):
    try:
        import win32com.client
        import pythoncom
        pythoncom.CoInitialize()
        word = win32com.client.Dispatch('Word.Application')
        word.Visible = False
        doc = word.Documents.Open(os.path.abspath(docx_path))
        doc.SaveAs(os.path.abspath(pdf_path), FileFormat=17) # 17 = wdFormatPDF
        doc.Close()
        word.Quit()
        pythoncom.CoUninitialize()
        print(f"PDF successfully converted at: {pdf_path}")
        return True
    except Exception as e:
        print(f"Error converting to PDF via Word: {e}")
        return False

if __name__ == "__main__":
    print("Generating vector C4 diagrams...")
    img_context = generate_context_diagram()
    img_container = generate_container_diagram()
    img_component = generate_component_diagram()
    img_dynamic = generate_dynamic_flow_diagram()

    imgs = {
        "context": img_context,
        "container": img_container,
        "component": img_component,
        "dynamic": img_dynamic
    }

    docs_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Docs")
    docx_file = os.path.join(docs_dir, "Bao_Cao_Kien_Truc_He_Thong_C4_Model.docx")
    pdf_file = os.path.join(docs_dir, "Bao_Cao_Kien_Truc_He_Thong_C4_Model.pdf")

    print("Building academic Word document...")
    build_docx_report(imgs, docx_file)

    print("Exporting to academic PDF...")
    convert_to_pdf(docx_file, pdf_file)
    print("Done!")
