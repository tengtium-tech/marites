import { Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthProvider';
import { SignInScreen } from './auth/SignInScreen';
import { AppShell } from './shell/AppShell';
import { SetupWizard } from './setup/SetupWizard';
import { QueueList } from './queues/QueueList';
import { QueueDetail } from './queues/QueueDetail';

export function App() {
  return (
    <AuthProvider>
      <SignedInOnly />
    </AuthProvider>
  );
}

/**
 * Nothing renders before the session is known: a flash of the console that then redirects is
 * the state that makes an operator think they were signed out mid-task.
 */
function SignedInOnly() {
  const { state, can } = useAuth();

  if (state.status === 'loading') {
    return (
      <div className="grid min-h-screen place-items-center text-[13px] text-muted-foreground">
        Loading session…
      </div>
    );
  }

  if (state.status !== 'authenticated') {
    return <SignInScreen />;
  }

  return (
    <AppShell>
      <Routes>
        <Route
          path="/setup"
          element={can('manage_brokers') ? <SetupWizard /> : <Navigate to="/queues" replace />}
        />
        <Route path="/queues" element={<QueueList />} />
        <Route path="/queues/:queueId" element={<QueueDetail />} />
        <Route path="*" element={<Navigate to="/queues" replace />} />
      </Routes>
    </AppShell>
  );
}
