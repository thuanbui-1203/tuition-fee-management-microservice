using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuitionService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Development/demo seed data. Every insert is guarded so re-applying the
            // migration is idempotent. Production environments should supply their own data.
            migrationBuilder.Sql("""
                INSERT INTO students (mssv, full_name, class_name, faculty, created_at, updated_at)
                VALUES
                    ('521H0001', 'Trần Thị B', 'DH22DT01', 'Công nghệ thông tin', now(), now()),
                    ('521H0002', 'Lê Văn C', 'DH22DT01', 'Công nghệ thông tin', now(), now()),
                    ('521H0003', 'Phạm Thị D', 'DH21QT02', 'Quản trị kinh doanh', now(), now())
                ON CONFLICT (mssv) DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO tuition_fees (student_id, semester, amount, status, paid_transaction_id, created_at, updated_at)
                SELECT s.id, '2024-2025/HK1', 8400000, 'UNPAID', NULL, now(), now()
                FROM students s
                WHERE s.mssv = '521H0001'
                  AND NOT EXISTS (SELECT 1 FROM tuition_fees f WHERE f.student_id = s.id AND f.semester = '2024-2025/HK1');
                """);

            migrationBuilder.Sql("""
                INSERT INTO tuition_fees (student_id, semester, amount, status, paid_transaction_id, created_at, updated_at)
                SELECT s.id, '2024-2025/HK1', 9200000, 'UNPAID', NULL, now(), now()
                FROM students s
                WHERE s.mssv = '521H0002'
                  AND NOT EXISTS (SELECT 1 FROM tuition_fees f WHERE f.student_id = s.id AND f.semester = '2024-2025/HK1');
                """);

            migrationBuilder.Sql("""
                INSERT INTO tuition_fees (student_id, semester, amount, status, paid_transaction_id, created_at, updated_at)
                SELECT s.id, '2023-2024/HK2', 7600000, 'PAID', '11111111-1111-1111-1111-111111111111', now(), now()
                FROM students s
                WHERE s.mssv = '521H0003'
                  AND NOT EXISTS (SELECT 1 FROM tuition_fees f WHERE f.student_id = s.id AND f.semester = '2023-2024/HK2');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM tuition_fees f
                USING students s
                WHERE f.student_id = s.id AND s.mssv IN ('521H0001', '521H0002', '521H0003');

                DELETE FROM students WHERE mssv IN ('521H0001', '521H0002', '521H0003');
                """);
        }
    }
}
