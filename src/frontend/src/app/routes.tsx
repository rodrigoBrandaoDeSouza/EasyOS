import { lazy, Suspense, type ComponentType } from 'react'
import { Center, Loader } from '@mantine/core'
import { createBrowserRouter, Navigate } from 'react-router'
import { LoginPage } from '../features/auth/LoginPage'
import { ForgotPasswordPage } from '../features/auth/ForgotPasswordPage'
import { AppLayout } from '../layout/AppLayout'
import { CompanyOnly, CustomerAreaGuard, HomeRedirect, RequireAuth } from './guards'

/** Carrega cada tela sob demanda (code splitting por rota). */
function page<M>(loader: () => Promise<M>, name: keyof M) {
  const Lazy = lazy(async () => ({ default: (await loader())[name] as ComponentType }))
  return function LazyPage() {
    return (
      <Suspense fallback={<Center py="xl"><Loader /></Center>}>
        <Lazy />
      </Suspense>
    )
  }
}

const CompanyDashboardPage = page(() => import('../features/dashboard/CompanyDashboardPage'), 'CompanyDashboardPage')
const CustomerDashboardPage = page(() => import('../features/dashboard/CustomerDashboardPage'), 'CustomerDashboardPage')
const CustomersPage = page(() => import('../features/customers/CustomersPage'), 'CustomersPage')
const CompanyUsersPage = page(() => import('../features/users/CompanyUsersPage'), 'CompanyUsersPage')
const CustomerUsersPage = page(() => import('../features/users/CustomerUsersPage'), 'CustomerUsersPage')
const VisitsPage = page(() => import('../features/visits/VisitsPage'), 'VisitsPage')
const CustomerVisitsPage = page(() => import('../features/visits/CustomerVisitsPage'), 'CustomerVisitsPage')
const CustomerOrdersPage = page(() => import('../features/orders/OrdersPage'), 'CustomerOrdersPage')
const OrdersPage = page(() => import('../features/orders/OrdersPage'), 'OrdersPage')
const OrderPage = page(() => import('../features/orders/OrderPage'), 'OrderPage')
const NewOrderPage = page(() => import('../features/orders/NewOrderPage'), 'NewOrderPage')
const ProductivityPage = page(() => import('../features/reports/ProductivityPage'), 'ProductivityPage')
const ReportPage = page(() => import('../features/reports/ReportPage'), 'ReportPage')
const FilesPage = page(() => import('../features/files/FilesPage'), 'FilesPage')
const CertificatesPage = page(() => import('../features/certificates/CertificatesPage'), 'CertificatesPage')
const CertificatePrintPage = page(() => import('../features/certificates/CertificatePrintPage'), 'CertificatePrintPage')
const MonthlyCommentsPage = page(() => import('../features/comments/MonthlyCommentsPage'), 'MonthlyCommentsPage')
const CompanySettingsPage = page(() => import('../features/settings/CompanySettingsPage'), 'CompanySettingsPage')
const CustomerSettingsPage = page(() => import('../features/settings/CustomerSettingsPage'), 'CustomerSettingsPage')

export const tenantRouter = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/esqueci-senha', element: <ForgotPasswordPage /> },

  // Páginas de impressão (sem menu).
  { path: '/imprimir/certificado/:orderId', element: <RequireAuth><CertificatePrintPage /></RequireAuth> },

  // Área da empresa prestadora.
  {
    element: (
      <RequireAuth>
        <CompanyOnly>
          <AppLayout area="company" />
        </CompanyOnly>
      </RequireAuth>
    ),
    children: [
      { path: '/painel', element: <CompanyDashboardPage /> },
      { path: '/clientes', element: <CustomersPage /> },
      { path: '/usuarios', element: <CompanyUsersPage /> },
      { path: '/visitas', element: <VisitsPage /> },
      { path: '/os', element: <OrdersPage /> },
      { path: '/os/nova', element: <NewOrderPage /> },
      { path: '/os/:orderId', element: <OrderPage /> },
      { path: '/produtividade', element: <ProductivityPage /> },
      { path: '/arquivos', element: <FilesPage /> },
      { path: '/configuracoes', element: <CompanySettingsPage /> },
    ],
  },

  // Área do cliente (usuário do cliente, ou usuário da empresa "acessando como").
  {
    path: '/c/:customerId',
    element: (
      <RequireAuth>
        <CustomerAreaGuard>
          <AppLayout area="customer" />
        </CustomerAreaGuard>
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="painel" replace /> },
      { path: 'painel', element: <CustomerDashboardPage /> },
      { path: 'usuarios', element: <CustomerUsersPage /> },
      { path: 'visitas', element: <CustomerVisitsPage /> },
      { path: 'os', element: <CustomerOrdersPage /> },
      { path: 'os/:orderId', element: <OrderPage /> },
      { path: 'arquivos', element: <FilesPage /> },
      { path: 'relatorios/:type', element: <ReportPage /> },
      { path: 'certificados', element: <CertificatesPage /> },
      { path: 'comentarios', element: <MonthlyCommentsPage /> },
      { path: 'configuracoes', element: <CustomerSettingsPage /> },
    ],
  },

  { path: '*', element: <RequireAuth><HomeRedirect /></RequireAuth> },
])
