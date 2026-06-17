# Tài liệu Thiết kế Cơ sở Dữ liệu (Database Schema Documentation)
**Hệ thống Quản lý Tài sản và Giám sát bằng UAV**

Tài liệu này mô tả chi tiết cấu trúc cơ sở dữ liệu của hệ thống, bao gồm sơ đồ thực thể liên kết (ERD) và từ điển dữ liệu (Data Dictionary) cho từng bảng.

## 1. Sơ đồ Thực thể Liên kết (ERD)

Đoạn mã dưới đây sử dụng định dạng Mermaid để hiển thị sơ đồ ER. Các nền tảng hỗ trợ Markdown (như GitHub, GitLab, Notion) sẽ tự động hiển thị sơ đồ này.

```mermaid
erDiagram
    %% ===================== USER & RBAC =====================
    Users {
        uuid user_id PK
        varchar username
        varchar password_hash
        varchar full_name
        varchar email
        varchar phone
        varchar status
        timestamp created_at
        timestamp updated_at
        timestamp deleted_at
    }

    Roles {
        int role_id PK
        varchar role_name
        varchar description
    }

    UserRoles {
        uuid user_id FK
        int role_id FK
        timestamp assigned_at
    }

    %% ===================== ASSET HIERARCHY =====================
    Regions {
        uuid region_asset_id PK
        varchar region_name
        geometry geom
        timestamp created_at
    }

    Substations {
        uuid substation_asset_id PK
        uuid region_asset_id FK
        varchar substation_name
        varchar voltage_level
        geometry geom
        timestamp created_at
    }

    TransmissionLines {
        uuid line_asset_id PK
        uuid substation_asset_id FK
        varchar line_name
        boolean is_critical_edge
        geometry geom
        timestamp created_at
    }

    Towers {
        uuid tower_id PK
        uuid line_asset_id FK
        varchar tower_code
        geometry geom
        timestamp created_at
    }

    Assets {
        uuid asset_id PK
        uuid tower_id FK
        varchar asset_type
        varchar asset_code
        varchar status
        float current_health_score
        varchar risk_level
        timestamp last_inspected_at
        timestamp created_at
    }

    AssetHealthHistories {
        uuid history_id PK
        uuid asset_id FK
        float health_score
        int active_defects_count
        jsonb calculation_log
        varchar risk_level
        timestamp calculated_at
    }

    %% ===================== UAV FLEET =====================
    UAVs {
        uuid uav_id PK
        varchar uav_code
        varchar model
        varchar status
        float battery_level
        geometry current_location
        timestamp last_maintenance_at
        timestamp created_at
        timestamp updated_at
    }

    %% ===================== MISSION =====================
    Missions {
        uuid mission_id PK
        varchar mission_code
        uuid manager_id FK
        uuid inspector_id FK
        uuid uav_id FK
        varchar status
        timestamp scheduled_start_at
        timestamp started_at
        timestamp ended_at
        text description
        timestamp created_at
        timestamp updated_at
    }

    MissionTargetLines {
        uuid mission_id FK
        uuid line_asset_id FK
        varchar status
    }

    MissionFlightLogs {
        uuid log_id PK
        uuid mission_id FK
        jsonb gps_track
        float min_battery_recorded
        float max_altitude_m
        int flight_duration_seconds
        varchar connection_status
        timestamp recorded_at
    }

    %% ===================== INSPECTION MEDIA =====================
    InspectionMedia {
        uuid media_id PK
        uuid mission_id FK
        uuid asset_id FK
        varchar media_type
        varchar file_url
        varchar ai_source
        varchar validation_status
        timestamp captured_at
        timestamp created_at
    }

    %% ===================== AI DEFECT DETECTION =====================
    DefectCategories {
        int category_id PK
        varchar category_code
        varchar category_name
        float severity_weight
        boolean is_emergency_class
        text description
    }

    DetectedAnomalies {
        uuid anomaly_id PK
        uuid media_id FK
        uuid asset_id FK
        int category_id FK
        uuid analyst_id FK
        jsonb bounding_box
        float confidence_score
        varchar validation_status
        varchar ai_source
        text analyst_notes
        timestamp validated_at
        timestamp created_at
    }

    %% ===================== EMERGENCY ALERTS =====================
    EmergencyAlerts {
        uuid alert_id PK
        uuid anomaly_id FK
        uuid asset_id FK
        uuid mission_id FK
        varchar status
        varchar priority
        int delivery_latency_seconds
        timestamp triggered_at
        timestamp received_at
        timestamp resolved_at
    }

    AlertEscalations {
        uuid escalation_id PK
        uuid alert_id FK
        uuid escalated_by FK
        uuid escalated_to FK
        text reason
        timestamp escalated_at
    }

    %% ===================== INCIDENT REPORTS =====================
    IncidentReports {
        uuid incident_id PK
        uuid mission_id FK
        uuid reported_by FK
        uuid asset_id FK
        varchar incident_type
        varchar severity
        text description
        varchar file_url
        varchar status
        timestamp reported_at
        timestamp created_at
    }

    %% ===================== MAINTENANCE TICKETS =====================
    MaintenanceTickets {
        uuid ticket_id PK
        varchar ticket_code
        uuid anomaly_id FK
        uuid asset_id FK
        uuid manager_id FK
        uuid technician_id FK
        varchar status
        varchar priority
        text description
        timestamp due_date
        timestamp assigned_at
        timestamp started_at
        timestamp resolved_at
        timestamp created_at
        timestamp updated_at
    }

    MaintenanceProofs {
        uuid proof_id PK
        uuid ticket_id FK
        uuid uploaded_by FK
        varchar file_url
        varchar after_repair_image_url
        text technician_notes
        timestamp uploaded_at
    }

    MaterialLogs {
        uuid material_log_id PK
        uuid ticket_id FK
        uuid logged_by FK
        varchar component_name
        varchar component_code
        int quantity_used
        varchar unit
        text field_observations
        timestamp logged_at
    }

    %% ===================== NOTIFICATIONS =====================
    Notifications {
        uuid notification_id PK
        uuid user_id FK
        varchar type
        varchar reference_type
        uuid reference_id
        varchar title
        text body
        boolean is_read
        timestamp sent_at
        timestamp read_at
    }

    %% ===================== AUDIT LOGS =====================
    AuditLogs {
        uuid log_id PK
        uuid user_id FK
        varchar table_name
        uuid record_id
        varchar action_type
        jsonb old_values
        jsonb new_values
        varchar ip_address
        varchar user_agent
        timestamp created_at
    }

    %% ===================== RELATIONSHIPS =====================
    Users ||--o{ UserRoles : "has"
    Roles ||--o{ UserRoles : "assigned_to"

    Regions ||--o{ Substations : "contains"
    Substations ||--o{ TransmissionLines : "comprises"
    TransmissionLines ||--o{ Towers : "has"
    Towers ||--o{ Assets : "hosts"

    Assets ||--o{ AssetHealthHistories : "logs_score"

    Users ||--o{ Missions : "manages"
    Users ||--o{ Missions : "executes"
    UAVs ||--o{ Missions : "dispatched_in"

    Missions ||--o{ MissionTargetLines : "targets"
    TransmissionLines ||--o{ MissionTargetLines : "included_in"

    Missions ||--|| MissionFlightLogs : "collects"

    Missions ||--o{ InspectionMedia : "captures"
    Assets ||--o{ InspectionMedia : "captured_in"

    InspectionMedia ||--o{ DetectedAnomalies : "contains"
    Assets ||--o{ DetectedAnomalies : "has_fault"
    DefectCategories ||--o{ DetectedAnomalies : "classified_as"
    Users ||--o{ DetectedAnomalies : "validates"

    DetectedAnomalies ||--o| EmergencyAlerts : "triggers"
    Assets ||--o{ EmergencyAlerts : "alerts_on"
    Missions ||--o{ EmergencyAlerts : "generated_in"
    EmergencyAlerts ||--o{ AlertEscalations : "has"
    Users ||--o{ AlertEscalations : "escalated_by"
    Users ||--o{ AlertEscalations : "escalated_to"

    Missions ||--o{ IncidentReports : "reported_in"
    Users ||--o{ IncidentReports : "submitted_by"
    Assets ||--o{ IncidentReports : "subject_of"

    DetectedAnomalies ||--o| MaintenanceTickets : "triggers"
    Assets ||--o{ MaintenanceTickets : "requires_work"
    Users ||--o{ MaintenanceTickets : "reviews"
    Users ||--o{ MaintenanceTickets : "assigned_to"

    MaintenanceTickets ||--o{ MaintenanceProofs : "verified_by"
    MaintenanceTickets ||--o{ MaterialLogs : "documents"
    Users ||--o{ MaintenanceProofs : "uploaded_by"
    Users ||--o{ MaterialLogs : "logged_by"

    Users ||--o{ Notifications : "receives"
    Users ||--o{ AuditLogs : "recorded_in"