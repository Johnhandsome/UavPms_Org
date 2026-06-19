**CAPSTONE PROJECT REGISTER**

Class:                                Duration time:  from …./…./…. to …./…./…..

(\*) Profession: **\<Software Engineer\>**          Specialty:  \<ES\>  \<IS\>  \<JS\>

(\*) Kind of registration:  Lecturer           Students

**1\. Register information for supervisor (if have)**

| \# | Full Name | Phone | E-Mail | Title |
| ----- | ----- | ----- | ----- | ----- |
| Supervisor 1 | Đặng Ngọc Minh Đức | 0989699299 | ducdnm2@fe.edu.vn | Assoc. Prof. |

**2\. Register information for students (if have)**

| \# | Full Name | Student Code | Phone | E-mail | Role |
| ----- | ----- | ----- | ----- | ----- | ----- |
| Student 1 | Nguyễn Quốc Khánh | SE193464 | 0708462865 | khanhnqse19@gmail.com | Leader |
| Student 2 | Phạm Hoàng Minh Châu | SE193418 | 0963274717 | phamhoangminhchau1973@gmail.com | Member |
| Student 3 | Nguyễn Nhật An | SE193338 | 0898520071 | an3439201@gmail.com | Member |
| Student 4 | Huỳnh Thái Liêm | SE193443 | 0932927074 | uselessliem@gmail.com | Member |

**3\. Register content of Capstone Project**

**3.1. Capstone Project name:**

**English:** Integrated UAV–AI Inspection and Maintenance Management System for Electrical Infrastructure

**Vietnamese:** Hệ thống UAV tích hợp AI để kiểm tra và quản lý bảo trì hạ tầng điện lực

**Abbreviation:** UAV-PMS

**Context**

The modern power-grid sector faces a convergence of two critical demands: efficient, data-driven asset maintenance and the need for reliable electricity supply. Traditional field inspections are labor-intensive, costly, and expose personnel to hazardous environments, while reactive maintenance strategies often result in delayed fault detection and increased operational risks.

Existing solutions remain fragmented, with separate systems for UAV surveying, defect recognition, and maintenance documentation. This lack of integration creates operational inefficiencies and data silos. Furthermore, many current platforms struggle with inconsistent inspection accuracy and insufficient capacity to analyze high volumes of imagery.

In addition to automated defect detection, the proposed platform aims to streamline inspection workflows, maintenance management, emergency response, and infrastructure monitoring through a unified software ecosystem. The system introduces an Asset Health Assessment mechanism that evaluates defect severity, inspection history, and maintenance records to support maintenance planning and decision making.

**Target Customers / Stakeholders:**

* Power utility companies

* Electrical infrastructure maintenance contractors

* Transmission line operators

* Smart grid management organizations

To address these challenges, the UAV-PMS project proposes a unified, intelligent platform for power-grid asset management. The system integrates UAV-enabled inspections with AI-driven analytics into a centralized, web-based workflow. By automating defect detection, providing emergency alerting, linking data to specific infrastructure assets, and offering actionable maintenance recommendations, UAV-PMS enables safer, faster, and more economical inspection processes.

Designed with a scalable architecture, the platform streamlines workflows for core stakeholders — Inspectors, Analysts, and Managers — through a single role-based web dashboard, bridging the gap between field-collected data and infrastructure asset management.

**Proposed Solution**

**A. Hybrid AI Architecture**

The system employs a two-tier AI processing model to balance real-time emergency response with comprehensive post-mission analysis:

**Tier 1 — Edge AI: Emergency Detection During Flight**

  Mission Start  
        ↓  
  Drone Inspection (Video Recording \+ GPS Logging)  
        ↓  
  Onboard Edge AI — Critical Defect Detection  
        ↓  (if critical defect detected)  
  Emergency Alert Generated  
        ↓  
  Control Center Notification (Real-time)

Edge AI is deployed on an onboard computing device such as Raspberry Pi 5, Jetson Orin Nano, or equivalent edge computing device, depending on available hardware resources. It focuses exclusively on high-priority, safety-critical defect categories:

* Fire / Thermal Anomaly

* Broken Conductor

* Fallen / Collapsed Tower

**Tier 2 — Cloud AI: Full Analysis After Mission**

  Mission End  
        ↓  
  Video / Image Upload to Server  
        ↓  
  Cloud AI Analysis (YOLOv8)  
        ↓  
  Analyst Validation (Human-in-the-Loop)  
        ↓  
  Defect Records → Asset Health Scoring  
        ↓  
  Maintenance Ticket Generation

Cloud AI performs comprehensive defect classification across all supported categories: Corrosion, Surface Crack, Vegetation Encroachment, Missing Components, Insulator Damage.

**B. Asset Health Assessment**

The system calculates an Asset Health Score (0–100) for each electrical asset using a configurable rule-based model:

| Factor | Weight |
| ----- | ----- |
| Defect Severity | 50% |
| Number of Active Defects | 20% |
| Maintenance History | 20% |
| Inspection Recency | 10% |

| Health Score | Risk Level | Action Priority |
| ----- | ----- | ----- |
| 80 – 100 | Low Risk | Routine monitoring |
| 60 – 79 | Medium Risk | Schedule maintenance |
| 40 – 59 | High Risk | Prioritize maintenance |
| 0 – 39 | Critical Risk | **Immediate action** |

*The Asset Health Score is calculated using a configurable rule-based model and does not require machine learning training.*

**C. Emergency Alert Workflow**

  New Alert (Edge AI triggered)  
        ↓  
  Analyst Review → Confirm / Reject / Escalate  
        ↓  
  Manager Notification  
        ↓  
  Maintenance Action Dispatched

**D. Verification Methods**

* **GPS-based Position Tagging (RTK — Future Enhancement):**

     The system utilizes GPS metadata embedded in UAV imagery to identify inspection locations. RTK positioning may be considered as a future enhancement for higher location precision.

* Asset Traceability: Each detected anomaly is mapped to specific infrastructure identifiers (Tower ID, component type), creating a traceable record of equipment conditions over time.

* Human-in-the-Loop Validation: The 'Analyst' role allows engineers to review, confirm, reject, or escalate AI-generated defect reports and emergency alerts.

* Maintenance Prioritization: The system generates maintenance tickets based on defect severity, type, and Asset Health Score.

**System Architecture**

UAV UNIT  
  ├─ Edge AI (Raspberry Pi 5 / Jetson Orin Nano) ──► Emergency Alert ──► Control Center  
  ├─ Video Recording  
  └─ GPS Logging  
          ↓  (Mission Completed)  
  Video/Image Upload  
          ↓  
  Cloud AI Analysis (YOLOv8 — Full Defect Detection)  
          ↓  
  Backend API (.NET)  
  ┌────────────────────────────────────────────┐  
  │  PostgreSQL  │  Asset Health Scoring       │  
  │  Emergency Alert Service                   │  
  │  Inspection Data Repository                │  
  │  Audit Logging                             │  
  └────────────────────────────────────────────┘  
          ↓  
  Web Dashboard  
  ┌────────────────────────────────────────────┐  
  │  GIS Map  │  Emergency Alerts  │  Health   │  
  │  Maintenance Tickets  │  Analytics         │  
  └────────────────────────────────────────────┘

**System Features**

* Multi-role Workflow: Inspectors, Analysts, Managers, and Maintenance Technicians each have a tailored role-based interface within a single web platform.

* Emergency Alert Management: Real-time critical anomaly notification with analyst review workflow (Confirm / Reject / Escalate), manager notification, and full alert tracking history.

* GIS-based Asset Monitoring: Map-based visualization of transmission towers, inspection locations, detected defects, active emergency alerts, and maintenance tickets using OpenStreetMap and LeafletJS.

* Inspection Data Repository: Centralized storage and management of inspection videos, images, and metadata — including search and retrieval by asset ID, date, mission, and defect type.

* Audit Logging: System-wide audit trail covering login history, alert actions, ticket modifications, and defect validation history.

* Status Tracking: End-to-end lifecycle visibility for inspection missions, anomalies, emergency alerts, and maintenance tickets.

* Analytics & Reporting: Defect trends, inspection coverage, asset health indicators, alert history, and maintenance summaries. Export in CSV, Excel, and PDF formats.

* Security & Data Protection: Role-Based Access Control (RBAC) and secure data handling.

**Functional Requirements**

* Authentication: Credential-based login (Username/Password). Accounts created and managed by System Administrator.

* User Management: Account management with strict Role-Based Access Control (RBAC).

* GIS Visualization: Map-based display of towers, defects, emergency alerts, and maintenance tickets with interactive filtering.

* Data Management: Video and image storage, metadata indexing, and search/retrieval for all inspection records.

* Audit Logging: Persistent log of user actions, alert responses, and ticket state changes.

* Reporting & Audits: Exportable reports in CSV, Excel, and PDF formats.

**3.2. Main proposal content (including result and product)**

**Software Deliverables:**

| \# | Module | Description |
| ----- | ----- | ----- |
| 1 | **Mission Management Module** | Create, schedule, dispatch, and track UAV inspection missions end-to-end. |
| 2 | **Asset Management Module** | Manage hierarchical power-grid asset registry (Regions → Substations → Lines → Towers). |
| 3 | **Emergency Alert Module** | Real-time critical defect alert notification, analyst review workflow (Confirm/Reject/Escalate), and alert history tracking. |
| 4 | **AI Defect Detection Module** | Cloud AI (YOLOv8) pipeline for comprehensive defect classification (5 categories); Edge AI integration for emergency detection (3 critical categories). |
| 5 | **GIS Monitoring Module** | Interactive map (OpenStreetMap \+ LeafletJS) for assets, defects, alerts, and maintenance tickets. |
| 6 | **Asset Health Scoring Module** | Rule-based health score calculation (0–100) with risk level classification and maintenance prioritization. |
| 7 | **Maintenance Ticket Module** | End-to-end maintenance workflow from defect validation to task closure with proof-of-work. |
| 8 | **Reporting Module** | Exportable analytics on defect trends, inspection coverage, alert history, and KPIs. |

**Technology Stack**

* Backend: .NET (ASP.NET Core Web API), Python (AI/Computer Vision services)

* Database: PostgreSQL

* Storage: File system or Object Storage for high-resolution imagery and video

* Communication — Primary: REST API, SignalR (real-time dashboard & alert updates)

* Communication — Optional: MQTT (UAV telemetry integration)

* Web Client: React / ASP.NET Core (responsive, mobile-friendly for Technicians)

* GIS: LeafletJS \+ OpenStreetMap

* Cloud AI: YOLOv8 — comprehensive defect detection (5 categories)

* Edge AI: YOLOv8n / YOLOv5n on Raspberry Pi 5 or Jetson Orin Nano — emergency detection (3 critical categories)

**Future Work / Out of Scope:**

* Custom UAV hardware development and flight controller tuning

* RTK high-precision positioning deployment

* Real-time video streaming

* Autonomous flight planning

**Dataset Sources (for AI Training):**

* Public UAV inspection datasets (open-source)

* Roboflow Universe datasets (electrical infrastructure defects)

* Open-source electrical infrastructure defect datasets

* Project-specific collected inspection images (during development)

**Project Assumptions:**

* UAV flight control is handled by existing UAV hardware and firmware.

* The project focuses on inspection management, AI-assisted defect analysis, and maintenance workflows.

* Autonomous navigation and custom UAV hardware development are outside the project scope.

**Functional Specification (Program):**

**Module 1 — Admin**

| \# | Function |
| ----- | ----- |
| **1.1** | Manage User Accounts: Create, update, suspend, and configure credentials for all roles (Inspectors, Analysts, Managers, Technicians). |
| **1.2** | System Authentication: Login/Logout with secure centralized credentials. |
| **1.3** | Profile Management: View and update personal profile information. |

**Module 2 — Manager**

| \# | Function |
| ----- | ----- |
| **2.1** | Power-Grid Asset Registry: Manage hierarchical infrastructure metadata (Regions, Substations, Transmission Lines, Tower IDs). |
| **2.2** | UAV Fleet Status: Monitor UAV availability and operational status. |
| **2.3** | Mission & Flight Dispatch: Schedule and dispatch inspection missions; assign UAV units and target grid sectors to Inspectors. |
| **2.4** | Maintenance Work Order Dispatch: Review validated defects, create work orders, and assign to Technicians. |
| **2.5** | System Audits & Reporting: Monitor analytics and export reports in CSV/Excel/PDF. |
| **2.6** | Asset Health Dashboard: Monitor infrastructure health scores and risk levels; identify Critical and High Risk assets. |
| **2.7** | Emergency Alert Dashboard: View active alerts, alert history, alert severity levels, and response status in real time. |

**Module 3 — Inspector**

| \# | Function |
| ----- | ----- |
| **3.1** | Mission Assignment Interface: View assigned inspection missions, inspection schedules, and target infrastructure assets. |
| **3.2** | Flight Status Monitoring: Monitor essential UAV status including GPS location, battery level, connection status, and mission progress. |
| **3.3** | Field Data Ingestion: Upload collected high-resolution imagery, video recordings, and drone flight logs to the centralized server. |
| **3.4** | Field Incident Dispatch: Submit manual reports on critical physical damage observed on-site or failed drone operations. |

**Module 4 — Analyst**

| \# | Function |
| ----- | ----- |
| **4.1** | Real-Time Anomaly Feeds: Receive instant notifications when severe defects or emergency conditions are flagged by AI services. |
| **4.2** | Computer Vision Evaluation: View, examine, and inspect YOLOv8-generated bounding boxes on structural images. |
| **4.3** | Human-in-the-Loop Validation: Review, edit, approve, or reject AI-generated defect outputs to ensure high-fidelity tracking. |
| **4.4** | Asset Lifecycle Tracking: Monitor individual infrastructure degradation history per Tower ID over time. |
| **4.5** | Asset Health Evaluation: Review Asset Health Scores and risk classifications; recommend maintenance priorities. |
| **4.6** | Emergency Alert Review: Receive edge AI-triggered alerts; confirm, reject, or escalate each alert with documented reasoning. |

**Module 5 — Maintenance Technician**

| \# | Function |
| ----- | ----- |
| **5.1** | Ticket Execution Interface: Receive and access assigned maintenance work orders with fault details and asset coordinates. |
| **5.2** | Workflow Status Management: Transition work orders (Assigned → In Progress → Pending Verification → Resolved). |
| **5.3** | Photographic Verification (Proof of Work): Capture and upload imagery of corrected assets as structural verification. |
| **5.4** | Material & Technical Reporting: Log component replacements, resource usage, and field observations before submitting for managerial closure. |

**Proposed Task Allocation for Students:**

| Student | Role | Responsibilities |
| ----- | ----- | ----- |
| **Student 1** | **Business Analyst & PM** | Manage project timeline; define system requirements; author URD, SRS, and UAT plans; design ticket routing and alert escalation workflows. |
| **Student 2** | **Backend Developer** | Develop .NET Web APIs; design PostgreSQL schema; build Authentication & RBAC; develop Asset Health Scoring Service; implement Emergency Alert Service and Audit Logging; manage Inspection Data Repository; deploy server infrastructure. |
| **Student 3** | **Frontend Developer** | Build responsive Web dashboard; implement GIS map visualization (LeafletJS); develop Asset Health and Emergency Alert dashboards; implement SignalR real-time updates; build Reporting module. |
| **Student 4** | **AI & Data Processing Engineer** | Prepare and curate datasets; integrate Edge AI emergency detection model (Raspberry Pi 5 / Jetson Orin Nano); integrate Cloud AI defect detection pipeline (YOLOv8); AI evaluation and testing. |

**Expected KPIs:**

| Category | Metric | Target |
| ----- | ----- | ----- |
| **AI Performance** | Cloud AI mAP (defect detection) | **≥ 80%** |
|  | Cloud AI Precision | **≥ 80%** |
|  | Cloud AI Recall | **≥ 75%** |
|  | Edge AI emergency detection accuracy | **≥ 85%** |
| **System Performance** | Image processing time (Cloud AI) | **\< 5 seconds per image** |
|  | Dashboard response time | **\< 2 seconds** |
|  | Emergency alert delivery latency | **\< 10 seconds (when network available)** |
| **Functional Performance** | Defect records linked to specific assets | **100%** |
|  | Automatic health score generation | **All inspected assets** |
|  | Emergency alert acknowledgment tracking | **100% of critical alerts** |
|  | User actions recorded in audit log | **100%** |

**4\. Other comments (propose all relevant things if any)**

…………………………………………………………………………………………………………………

| Supervisor (If have) *(Sign and full name)*  | ……………., date …./…../…………. On behalf of the Registers *(Sign and full name)*  |
| ----- | ----- |

