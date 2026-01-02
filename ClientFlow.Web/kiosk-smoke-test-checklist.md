# Kiosk Smoke Test Checklist

- [ ] Open kiosk runner (`/kiosk/runner.html?code=liberty-nps`).
- [ ] Fill required fields (phone, staff, service, satisfaction, timeliness, professionalism, recommend, follow-up).
- [ ] Submit the survey.
- [ ] Verify the success/thank-you screen appears.
- [ ] Verify the submission payload includes the Liberty extended keys (`visit_reason`, `visit_other`, `resolved_today`, `service_rating`, `services_used`, `recommend_score`, `additional_feedback`).
- [ ] Wait for idle timeout and confirm the kiosk resets to the start screen.
