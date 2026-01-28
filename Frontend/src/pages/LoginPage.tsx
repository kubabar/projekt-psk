import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { authAPI } from '../services/api';
import { useAuth } from '../contexts/AuthContext';

export const LoginPage = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [sessionId, setSessionId] = useState('');
  const [showCodeModal, setShowCodeModal] = useState(false);
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await authAPI.login(email, password);
      
      if (response.data.success && response.data.data) {
        setSessionId(response.data.data.sessionId);
        await authAPI.sendCode(response.data.data.sessionId, email);
        setShowCodeModal(true);
      } else {
        setError(response.data.error || 'Błąd logowania');
      }
    } catch (err: any) {
      setError(err.response?.data?.error || 'Błąd połączenia');
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyCode = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const response = await authAPI.verifyCode(sessionId, code);
      
      if (response.data.success && response.data.data) {
        await login({
          userId: response.data.data.userId,
          email,
          token: response.data.data.token
        });
        navigate('/');
      } else {
        setError(response.data.error || 'Nieprawidłowy kod');
      }
    } catch (err: any) {
      setError(err.response?.data?.error || 'Błąd weryfikacji');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <h1 style={{ marginBottom: '30px' }}>Logowanie</h1>
      
      {error && <div className="error">{error}</div>}
      
      {!showCodeModal ? (
        <form onSubmit={handleLogin}>
          <div className="form-group">
            <label>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              disabled={loading}
            />
          </div>
          
          <div className="form-group">
            <label>Hasło</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              disabled={loading}
            />
          </div>
          
          <button type="submit" disabled={loading}>
            {loading ? 'Logowanie...' : 'Zaloguj się'}
          </button>
          
          <div style={{ marginTop: '20px', textAlign: 'center' }}>
            <Link to="/register" className="link">Utwórz konto</Link>
            {' | '}
            <Link to="/request-reset" className="link">Zapomniałem hasła</Link>
          </div>
        </form>
      ) : (
        <div>
          <h3>Kod weryfikacyjny 2FA</h3>
          <div className="info" style={{ marginBottom: '20px' }}>
            Kod został wysłany na {email}
          </div>
          
          <form onSubmit={handleVerifyCode}>
            <div className="form-group">
              <label>Wprowadź kod 6-cyfrowy</label>
              <input
                type="text"
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/[^0-9]/g, ''))}
                maxLength={6}
                required
                disabled={loading}
                autoFocus
              />
            </div>
            
            <button type="submit" disabled={loading}>
              {loading ? 'Weryfikacja...' : 'Weryfikuj'}
            </button>
            
            <button 
              type="button" 
              className="secondary"
              onClick={() => setShowCodeModal(false)}
              disabled={loading}
            >
              Anuluj
            </button>
          </form>
        </div>
      )}
    </div>
  );
};
