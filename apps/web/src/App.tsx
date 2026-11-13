import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './shell/AppShell';
import { SetupWizard } from './setup/SetupWizard';
import { QueueList } from './queues/QueueList';

export function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/setup" element={<SetupWizard />} />
        <Route path="/queues" element={<QueueList />} />
        <Route path="*" element={<Navigate to="/setup" replace />} />
      </Routes>
    </AppShell>
  );
}
