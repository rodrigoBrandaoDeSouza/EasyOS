// Tipos das respostas da API (espelham os DTOs do backend, em camelCase).

export type UserType = 'company' | 'customer'

export interface Me {
  id: number
  name: string
  email: string
  type: UserType
  profile: number
  companyId: number | null
  customerId: number | null
  customerName: string | null
}

export interface Branding {
  name: string
  primaryColor: string
  headingColor: string
  otherPlaguesLabel: string
  logoUrl: string | null
  loginBackgroundUrl: string | null
  faviconUrl: string | null
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface LookupItem {
  id: number
  name: string
  active: boolean
}

export interface Company {
  id: number
  tradingName: string
  companyName: string
  cpfCnpj: string | null
  address: string
  number: number
  complement: string
  neighborhood: string
  city: string
  state: string
  zipcode: string
  phone: string
  fax: string | null
  email: string | null
  contactName: string
  charter: string
  charterExpiration: string
  ineaCode: string | null
  environmentalPermit: string | null
  environmentalPermitExpiration: string | null
  legalRepresentative: string | null
  useBaitings: boolean
  usePlaqueOfGlue: boolean
  useLuminousTrap: boolean
  usePheromoneTrap: boolean
}

export interface TechnicalManager {
  id: number
  state: string
  register: string
  name: string
}

export interface CompanyUser {
  id: number
  name: string
  email: string
  phone: string | null
  department: string
  profile: number
  active: boolean
  hasSignature: boolean
  createdDate: string
}

export interface Customer {
  id: number
  tradingName: string
  companyName: string
  cpfCnpj: string | null
  address: string
  number: number
  complement: string
  neighborhood: string
  city: string
  state: string
  zipcode: string
  phone: string
  contactName: string
  contactFunction: string | null
  fax: string | null
  email: string | null
  activityType: string | null
  clientHasContract: boolean
  certificateDate: number
  observation: string | null
  latitude: number
  longitude: number
  active: boolean
  companyId: number
  missingGeolocation: boolean
}

export interface CustomerUser {
  id: number
  customerId: number
  name: string
  email: string
  department: string | null
  profile: number
  active: boolean
  createdDate: string
}

export interface Plague {
  id: number
  name: string
  plagueType: string
  active: boolean
}

export interface PlagueStatus {
  id: number
  description: string
}

export interface ProductListItem {
  id: number
  name: string
  productTypeId: number
  unity: string
  productCode: number
  registrationNumber: string
  chemicalGroup: string
  activeIngredient: string
  equipmentType: string
  symptoms: string
  antidotes: string
  active: boolean
}

export interface ProductDetail {
  id: number
  name: string
  productTypeId: number
  productCode: number
  registrationNumber: string
  chemicalGroup: string
  activeIngredient: string
  equipmentType: string
  symptoms: string
  antidotes: string
  plagues: { plagueId: number; plagueName: string | null; concentration: number }[]
  applications: { id: number | null; description: string }[]
  activeIngredients: { id: number | null; activeIngredient: string; concentration: number }[]
}

export interface ProductType {
  id: number
  type: string
  unity: string
}

export interface ProductForPlague {
  plagueProductId: number
  productId: number
  productTypeId: number
  name: string
  unity: string
  concentration: number
  applications: { id: number; description: string }[]
}

export interface Parameter {
  id: number
  name: string
  description: string
}

export interface VisitListItem {
  id: number
  customerId: number
  customerName: string
  visitDate: string
  checkinDate: string | null
  offsetMinutes: number
  technicianId: number | null
  technicianName: string | null
  technicianPhone: string | null
  responsibleId: number
  responsibleName: string | null
  description: string | null
  treatedAreas: string | null
  auxiliarTechnicians: string[]
  certificateDate: number | null
  active: boolean
  hasConfirmed: boolean
  isLate: boolean
  status: string
  orderId: number
  orderSequence: number | null
  address: string
  number: number
  complement: string
  neighborhood: string
  city: string
  state: string
  zipcode: string
}

export interface CalendarItem {
  id: number
  visitDate: string
  customerName: string
  technicianName: string | null
  done: boolean
}

export interface OrderListItem {
  id: number
  sequence: number
  orderDate: string
  timeStart: string
  timeEnd: string
  customerId: number
  customerName: string
  technicianId: number
  technicianName: string
  serviceExecuted: boolean
  active: boolean
  observations: string | null
}

export interface OrderDetailsFlags {
  hadEvidenceOfPlague: boolean
  hadEquipmentRevision: boolean
  hadBaiting: boolean
  hadPlaquesOfGlue: boolean
  hadLuminousTrap: boolean
  customerNeedPlagueAccount: boolean
  hadPheromoneTrap: boolean
  hadProductConsumption: boolean
}

export interface TrapItem {
  type: string | null
  point: string | null
  quantity: number
}

export interface OrderCustomerInfo {
  id: number
  tradingName: string
  companyName: string
  cpfCnpj: string | null
  address: string
  number: number
  complement: string
  neighborhood: string
  city: string
  state: string
  zipcode: string
  activityType: string | null
  certificateDate: number
}

export interface OrderCompanyInfo {
  id: number
  companyName: string
  tradingName: string
  cpfCnpj: string | null
  charter: string
  charterExpiration: string
  address: string
  number: number
  neighborhood: string
  city: string
  state: string
  zipcode: string
  phone: string
}

export interface OrderDetail {
  id: number
  sequence: number
  active: boolean
  orderDate: string
  timeStart: string
  timeEnd: string
  checkinDate: string | null
  offsetMinutes: number
  visitId: number
  visitDescription: string | null
  treatedAreas: string | null
  serviceExecuted: boolean
  reasonOfNonExecution: string | null
  observations: string | null
  responsibleName: string | null
  responsibleBusiness: string | null
  hasResponsibleSignature: boolean
  technicianId: number
  technicianName: string
  hasTechnicianSignature: boolean
  technicalResponsible: string | null
  customer: OrderCustomerInfo
  company: OrderCompanyInfo
  details: OrderDetailsFlags
  plagues: { plagueId: number; plagueName: string; plagueStatusId: number; plagueStatus: string; location: string | null; quantity: number }[]
  baitings: TrapItem[]
  plaquesOfGlue: (TrapItem & { rodentQuantity: number })[]
  pheromones: TrapItem[]
  luminous: { point: string | null; quantity: number; plagueCounts: { plagueId: number; plagueName: string; quantity: number }[] }[]
  consumptions: {
    plagueProductId: number
    plagueName: string
    productName: string
    unity: string
    concentration: number
    productApplicationId: number
    applicationName: string
    quantity: number
    lot: string
    expirationDate: string | null
  }[]
  nonConformities: { id: number; place: string | null; description: string | null; suggestion: string | null }[]
  evidenceIds: number[]
}

export interface ResponsibleSignature {
  name: string | null
  business: string | null
  signature: string | null
}

export interface CertificateListItem {
  orderId: number
  sequence: number
  startDate: string
  endDate: string
}

export interface Certificate {
  orderId: number
  sequence: number
  orderDate: string
  timeStart: string
  timeEnd: string
  validFrom: string
  validUntil: string
  validityDays: number
  responsibleName: string | null
  responsibleBusiness: string | null
  customer: OrderCustomerInfo
  company: OrderCompanyInfo
  technicianName: string
  technicalManager: { state: string; name: string; register: string } | null
  treatedAreas: string | null
  products: {
    id: number
    name: string
    chemicalGroup: string
    activeIngredient: string
    registrationNumber: string
    symptoms: string
    antidotes: string
    unity: string
    targetPlagues: string
  }[]
  plaguesFound: string[]
}

export interface ChartSeries {
  name: string
  type: 'column' | 'line'
  data: number[]
}

export interface MonthlyReport {
  title: string
  categories: string[]
  series: ChartSeries[]
  table: { headers: string[]; rows: string[][] }
  drillDowns: { category: string; items: { name: string; quantity: number }[] }[] | null
  luminous: { category: string; totals: number[]; points: { point: string; values: number[] }[] }[] | null
  luminousColumns: string[] | null
}

export interface CustomerDashboard {
  from: string
  to: string
  plaguesFound: MonthlyReport
  rodents: MonthlyReport
  consumption: MonthlyReport
}

export interface CompanyDashboard {
  from: string
  to: string
  technicians: number
  serviceOrders: number
  activeCustomers: number
  days: string[]
  ordersPerDay: number[]
  ordersPerTechnician: { name: string; quantity: number }[]
  consumptionByProductType: { name: string; quantity: number }[]
}

export interface NonConformityReportItem {
  orderId: number
  sequence: number
  orderDate: string
  place: string | null
  description: string | null
  suggestion: string | null
}

export interface ProductivityItem {
  technicianId: number
  technicianName: string
  hoursWorked: number
  minutesWorked: number
  scheduled: number
  accomplished: number
  notAccomplished: number
  pending: number
}

export interface MonthlyComments {
  year: number
  months: { month: number; comment: string | null; author: string | null; createdAt: string | null }[]
}

export interface FolderListing {
  path: string
  folders: { name: string; path: string }[]
  files: { name: string; path: string; size: number; lastModifiedUtc: string }[]
}
