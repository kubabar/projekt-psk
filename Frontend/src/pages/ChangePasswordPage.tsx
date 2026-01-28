import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authAPI } from '../services/api';

export const ChangePasswordPage = () => {
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (newPassword !== confirmPassword) {
      setError('Nowe hasła nie są identyczne');
      return;
    }

    if (newPassword.length < 8) {
      setError('Nowe hasło musi mieć minimum 8 znaków');
      return;
    }

    setLoading(true);

    try {
      const response = await authAPI.changePassword(oldPassword, newPassword);
      
      if (response.data.success) {
        setSuccess(true);
        setTimeout(() => navigate('/'), 2000);
      } else {
        setError(response.data.error || 'Błąd zmiany hasła');
      }
    } catch (err: any) {
      setError(err.response?.data?.error || 'Błąd połączenia');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <h1 style={{ marginBottom: '30px' }}>Zmiana hasła</h1>
      
      {error && <div className="error">{error}</div>}
      {success && <div className="success">Hasło zmienione! Przekierowanie...</div>}
      
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Stare hasło</label>
          <input
            type="password"
            value={oldPassword}
            onChange={(e) => setOldPassword(e.target.value)}
            required
            disabled={loading || success}
          />
        </div>
        
        <div className="form-group">
          <label>Nowe hasło (min. 8 znaków)</label>
          <input
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            minLength={8}
            required
            disabled={loading || success}
          />
        </div>
        
        <div className="form-group">
          <label>Potwierdź nowe hasło</label>
          <input
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            disabled={loading || success}
          />
        </div>
        
        <button type="submit" disabled={loading || success}>
          {loading ? 'Zmiana...' : 'Zmień hasło'}
        </button>
        
        <button 
          type="button"
          className="secondary"
          onClick={() => navigate('/')}
          disabled={loading}
        >
          Anuluj
        </button>
      </form>
    </div>
  );
};
