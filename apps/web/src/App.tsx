import { Navigate, Route, Routes } from 'react-router-dom';
import { SetupWizard } from './setup/SetupWizard';

export function App() {
  return (
    <Routes>
      <Route path="/setup" element={<SetupWizard />} />
      <Route path="*" element={<Navigate to="/setup" replace />} />
    </Routes>
  );
}
