-- Creates one logical database per microservice. Executed only on the first container start.
CREATE DATABASE tuition_db;
CREATE DATABASE otp_db;
CREATE DATABASE notification_db;

-- Placeholders for services outside the current .NET migration scope (user/payment).
CREATE DATABASE user_db;
CREATE DATABASE payment_db;
