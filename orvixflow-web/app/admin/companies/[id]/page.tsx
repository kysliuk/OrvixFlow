"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, Building, Users, CreditCard, AlertTriangle, CheckCircle, PauseCircle } from "lucide-react";

interface CompanyData {
  company: {
    id: string;
    name: string;
    plan: string;
    subscriptionStatus: string;
    createdAt: string;
    userCount: number;
    members: Array<{
      id: string;
      email: string;
      role: string;
      createdAt: string;
    }>;
  };
  subscription: {
    id: string;
    status: string;
    billingInterval: string;
    currentPeriodStart: string;
    currentPeriodEnd: string;
    trialEndsAt: string | null;
    plan: {
      id: string;
      name: string;
      slug: string;
      monthlyPriceCents: number;
      maxSeats: number | null;
    };
  } | null;
  entitlements: {
    maxSeats: number | null;
    maxMonthlyTokens: number;
    maxApiRequestsPerDay: number;
    maxStorageMb: number;
    maxKnowledgeBases: number;
    tokensUsedThisPeriod: number;
    apiRequestsUsedToday: number;
    storageUsedMb: number;
    knowledgeBasesCount: number;
  };
}

export default function CompanyDetailPage() {
  const { data: session } = useSession();
  const params = useParams();
  const router = useRouter();
  const [company, setCompany] = useState<CompanyData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if ((session as any)?.apiToken && params.id) {
      fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${params.id}`, {
        headers: { "Authorization": `Bearer ${(session as any).apiToken}` }
      })
        .then(res => {
          if (!res.ok) throw new Error("Failed to load company");
          return res.json();
        })
        .then(data => {
          setCompany(data);
          setLoading(false);
        })
        .catch(e => {
          setError(e.message);
          setLoading(false);
        });
    }
  }, [session, params.id]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case "Active": return "text-success bg-success/10";
      case "Trialing": return "text-primary bg-primary/10";
      case "PastDue": return "text-warning bg-warning/10";
      case "Suspended": return "text-danger bg-danger/10";
      default: return "text-white/50 bg-white/10";
    }
  };

  const formatDate = (date: string) => {
    return new Date(date).toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric"
    });
  };

  const getUsagePercentage = (used: number, max: number) => {
    if (max === 0) return 0;
    return Math.min(100, (used / max) * 100);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-danger animate-pulse font-mono">LOADING_COMPANY_DATA...</div>
      </div>
    );
  }

  if (error || !company) {
    return (
      <div className="flex flex-col items-center justify-center h-64 gap-4">
        <div className="text-danger">{error || "Company not found"}</div>
        <button 
          onClick={() => router.back()}
          className="px-4 py-2 bg-white/10 text-white rounded-lg text-sm"
        >
          Go Back
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 max-w-6xl h-full">
      <button 
        onClick={() => router.back()}
        className="flex items-center gap-2 text-white/60 hover:text-white text-sm transition-colors w-fit"
      >
        <ArrowLeft className="w-4 h-4" /> Back to Companies
      </button>

      <div className="flex justify-between items-start">
        <div>
          <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
            <Building className="w-6 h-6 text-danger" /> {company.company.name}
          </h1>
          <p className="text-sm text-white/60 font-mono">
            ID: {company.company.id}
          </p>
        </div>
        <span className={`px-3 py-1 rounded-full text-xs font-bold flex items-center gap-1 ${getStatusColor(company.subscription?.status || "None")}`}>
          {company.subscription?.status === "Active" && <CheckCircle className="w-3 h-3" />}
          {company.subscription?.status === "Trialing" && <AlertTriangle className="w-3 h-3" />}
          {company.subscription?.status === "Suspended" && <PauseCircle className="w-3 h-3" />}
          {company.subscription?.status || "No Subscription"}
        </span>
      </div>

      {company.subscription?.status === "Trialing" && company.subscription.trialEndsAt && (
        <div className="bg-primary/10 border border-primary/30 p-4 rounded-xl flex items-center gap-3">
          <AlertTriangle className="w-5 h-5 text-primary" />
          <div>
            <h3 className="font-medium text-primary">Trial Period</h3>
            <p className="text-sm text-white/70">
              Trial ends on {formatDate(company.subscription.trialEndsAt)}
            </p>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
            <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
              <CreditCard className="w-5 h-5 text-danger" /> Subscription
            </h2>
            {company.subscription ? (
              <div className="space-y-4">
                <div className="flex justify-between items-center p-4 bg-white/5 rounded-lg">
                  <div>
                    <p className="text-sm text-white/50">Current Plan</p>
                    <p className="text-lg font-semibold text-white">{company.subscription.plan.name}</p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm text-white/50">Billing</p>
                    <p className="text-white capitalize">{company.subscription.billingInterval}</p>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="p-4 bg-white/5 rounded-lg">
                    <p className="text-sm text-white/50">Period Start</p>
                    <p className="text-white">{formatDate(company.subscription.currentPeriodStart)}</p>
                  </div>
                  <div className="p-4 bg-white/5 rounded-lg">
                    <p className="text-sm text-white/50">Period End</p>
                    <p className="text-white">{formatDate(company.subscription.currentPeriodEnd)}</p>
                  </div>
                </div>
              </div>
            ) : (
              <p className="text-white/50">No active subscription</p>
            )}
          </div>

          <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
            <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
              <Users className="w-5 h-5 text-danger" /> Members ({company.company.userCount})
            </h2>
            <div className="space-y-2">
              {company.company.members.map(member => (
                <div key={member.id} className="flex justify-between items-center p-3 bg-white/5 rounded-lg">
                  <div>
                    <p className="text-white">{member.email}</p>
                    <p className="text-xs text-white/50">{member.id}</p>
                  </div>
                  <span className="px-2 py-1 bg-white/10 text-white/70 text-xs rounded">
                    {member.role}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
            <h2 className="text-lg font-semibold mb-4">Usage & Limits</h2>
            <div className="space-y-4">
              <div>
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-white/50">Seats</span>
                  <span className="text-white">{company.company.userCount} / {company.entitlements.maxSeats || "∞"}</span>
                </div>
                <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-danger transition-all"
                    style={{ width: `${getUsagePercentage(company.company.userCount, company.entitlements.maxSeats || 100)}%` }}
                  />
                </div>
              </div>
              <div>
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-white/50">Tokens (Monthly)</span>
                  <span className="text-white">{(company.entitlements.tokensUsedThisPeriod / 1000).toFixed(1)}k / {(company.entitlements.maxMonthlyTokens / 1000).toFixed(0)}k</span>
                </div>
                <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-primary transition-all"
                    style={{ width: `${getUsagePercentage(company.entitlements.tokensUsedThisPeriod, company.entitlements.maxMonthlyTokens)}%` }}
                  />
                </div>
              </div>
              <div>
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-white/50">API Requests (Today)</span>
                  <span className="text-white">{company.entitlements.apiRequestsUsedToday} / {company.entitlements.maxApiRequestsPerDay}</span>
                </div>
                <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-success transition-all"
                    style={{ width: `${getUsagePercentage(company.entitlements.apiRequestsUsedToday, company.entitlements.maxApiRequestsPerDay)}%` }}
                  />
                </div>
              </div>
              <div>
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-white/50">Knowledge Bases</span>
                  <span className="text-white">{company.entitlements.knowledgeBasesCount} / {company.entitlements.maxKnowledgeBases}</span>
                </div>
                <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                  <div 
                    className="h-full bg-warning transition-all"
                    style={{ width: `${getUsagePercentage(company.entitlements.knowledgeBasesCount, company.entitlements.maxKnowledgeBases)}%` }}
                  />
                </div>
              </div>
            </div>
          </div>

          <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
            <h2 className="text-lg font-semibold mb-4">Actions</h2>
            <div className="space-y-2">
              <button className="w-full px-4 py-2 bg-danger/10 text-danger text-sm font-medium rounded-lg hover:bg-danger/20 transition-colors">
                Change Plan
              </button>
              {company.subscription?.status === "Active" && (
                <button className="w-full px-4 py-2 bg-white/5 text-white/70 text-sm font-medium rounded-lg hover:bg-white/10 transition-colors">
                  Cancel Subscription
                </button>
              )}
              {company.subscription?.status === "Suspended" && (
                <button className="w-full px-4 py-2 bg-success/10 text-success text-sm font-medium rounded-lg hover:bg-success/20 transition-colors">
                  Reactivate
                </button>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
