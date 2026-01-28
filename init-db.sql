-- Database schema for Auth System
-- This file will be automatically executed when MySQL container starts

USE auth_system;

-- Users table
CREATE TABLE IF NOT EXISTS users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    password_salt VARCHAR(255) NOT NULL,
    password_expires_at DATETIME,
    failed_login_attempts INT DEFAULT 0,
    locked_until DATETIME NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_email (email),
    INDEX idx_locked_until (locked_until)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Password history table
CREATE TABLE IF NOT EXISTS password_history (
    history_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    password_salt VARCHAR(255) NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    INDEX idx_user_created (user_id, created_at DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Login sessions table
CREATE TABLE IF NOT EXISTS login_sessions (
    session_id VARCHAR(255) PRIMARY KEY,
    user_id INT NOT NULL,
    ip_address VARCHAR(45),
    user_agent TEXT,
    is_verified BOOLEAN DEFAULT FALSE,
    expires_at DATETIME NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    INDEX idx_user_verified (user_id, is_verified),
    INDEX idx_expires_at (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Verification codes table (2FA)
CREATE TABLE IF NOT EXISTS verification_codes (
    code_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    code_hash VARCHAR(255) NOT NULL,
    code_salt VARCHAR(255) NOT NULL,
    is_used BOOLEAN DEFAULT FALSE,
    expires_at DATETIME NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    INDEX idx_user_valid (user_id, is_used, expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Password reset tokens table
CREATE TABLE IF NOT EXISTS password_reset_tokens (
    token_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    token_salt VARCHAR(255) NOT NULL,
    ip_address VARCHAR(45),
    is_used BOOLEAN DEFAULT FALSE,
    expires_at DATETIME NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    INDEX idx_user_valid (user_id, is_used, expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Stored Procedure: Register User
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_register_user$$

CREATE PROCEDURE sp_register_user(
    IN p_email VARCHAR(255),
    IN p_password_hash VARCHAR(255),
    IN p_password_salt VARCHAR(255),
    IN p_validity_days INT,
    OUT p_success BOOLEAN,
    OUT p_error VARCHAR(255)
)
BEGIN
    DECLARE v_user_count INT;
    DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1
            p_error = MESSAGE_TEXT;
        SET p_success = FALSE;
        ROLLBACK;
    END;
    
    START TRANSACTION;
    
    -- Check if user already exists
    SELECT COUNT(*) INTO v_user_count FROM users WHERE email = p_email;
    
    IF v_user_count > 0 THEN
        SET p_success = FALSE;
        SET p_error = 'User already exists';
        ROLLBACK;
    ELSE
        -- Insert new user
        INSERT INTO users (email, password_hash, password_salt, password_expires_at)
        VALUES (p_email, p_password_hash, p_password_salt, 
                DATE_ADD(UTC_TIMESTAMP(), INTERVAL p_validity_days DAY));
        
        SET p_success = TRUE;
        SET p_error = NULL;
        COMMIT;
    END IF;
END$$

-- Stored Procedure: Change Password
DROP PROCEDURE IF EXISTS sp_change_password$$

CREATE PROCEDURE sp_change_password(
    IN p_user_id INT,
    IN p_new_hash VARCHAR(255),
    IN p_new_salt VARCHAR(255),
    IN p_validity_days INT,
    OUT p_success BOOLEAN,
    OUT p_error VARCHAR(255)
)
BEGIN
    DECLARE v_old_hash VARCHAR(255);
    DECLARE v_old_salt VARCHAR(255);
    
    DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1
            p_error = MESSAGE_TEXT;
        SET p_success = FALSE;
        ROLLBACK;
    END;
    
    START TRANSACTION;
    
    -- Get current password
    SELECT password_hash, password_salt INTO v_old_hash, v_old_salt
    FROM users WHERE user_id = p_user_id;
    
    -- Save old password to history
    INSERT INTO password_history (user_id, password_hash, password_salt)
    VALUES (p_user_id, v_old_hash, v_old_salt);
    
    -- Update user password
    UPDATE users 
    SET password_hash = p_new_hash,
        password_salt = p_new_salt,
        password_expires_at = DATE_ADD(UTC_TIMESTAMP(), INTERVAL p_validity_days DAY),
        failed_login_attempts = 0,
        locked_until = NULL
    WHERE user_id = p_user_id;
    
    SET p_success = TRUE;
    SET p_error = NULL;
    COMMIT;
END$$

-- Stored Procedure: Reset Password with Token
DROP PROCEDURE IF EXISTS sp_reset_password_with_token$$

CREATE PROCEDURE sp_reset_password_with_token(
    IN p_token_id INT,
    IN p_new_hash VARCHAR(255),
    IN p_new_salt VARCHAR(255),
    IN p_validity_days INT,
    OUT p_success BOOLEAN,
    OUT p_error VARCHAR(255)
)
BEGIN
    DECLARE v_user_id INT;
    DECLARE v_old_hash VARCHAR(255);
    DECLARE v_old_salt VARCHAR(255);
    
    DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1
            p_error = MESSAGE_TEXT;
        SET p_success = FALSE;
        ROLLBACK;
    END;
    
    START TRANSACTION;
    
    -- Get user_id from token
    SELECT user_id INTO v_user_id
    FROM password_reset_tokens
    WHERE token_id = p_token_id;
    
    -- Get current password
    SELECT password_hash, password_salt INTO v_old_hash, v_old_salt
    FROM users WHERE user_id = v_user_id;
    
    -- Save old password to history
    INSERT INTO password_history (user_id, password_hash, password_salt)
    VALUES (v_user_id, v_old_hash, v_old_salt);
    
    -- Update user password
    UPDATE users 
    SET password_hash = p_new_hash,
        password_salt = p_new_salt,
        password_expires_at = DATE_ADD(UTC_TIMESTAMP(), INTERVAL p_validity_days DAY),
        failed_login_attempts = 0,
        locked_until = NULL
    WHERE user_id = v_user_id;
    
    -- Mark token as used
    UPDATE password_reset_tokens
    SET is_used = TRUE
    WHERE token_id = p_token_id;
    
    SET p_success = TRUE;
    SET p_error = NULL;
    COMMIT;
END$$

DELIMITER ;

-- Cleanup job (optional - run periodically)
-- DELETE FROM login_sessions WHERE expires_at < UTC_TIMESTAMP();
-- DELETE FROM verification_codes WHERE expires_at < UTC_TIMESTAMP();
-- DELETE FROM password_reset_tokens WHERE expires_at < UTC_TIMESTAMP();

-- Grant privileges
GRANT ALL PRIVILEGES ON auth_system.* TO 'auth_app'@'%';
FLUSH PRIVILEGES;
