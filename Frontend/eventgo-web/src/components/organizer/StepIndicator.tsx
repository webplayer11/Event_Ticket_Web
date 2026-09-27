import React from 'react';

interface StepIndicatorProps {
    currentStep: number;
    onSave?: () => void;
    onContinue?: () => void;
}

const steps = [
    'Thông tin sự kiện',
    'Thời gian & Loại vé',
    'Cài đặt',
    'Thông tin thanh toán',
];

export const StepIndicator: React.FC<StepIndicatorProps> = ({ currentStep, onSave, onContinue }) => {
    return (
        <div className="mb-8 flex items-center justify-between border-b border-[#2d2d2d] pb-4">
            <div className="flex items-center gap-8">
                {steps.map((step, index) => {
                    const stepNumber = index + 1;
                    const isActive = currentStep === stepNumber;
                    const isPassed = currentStep > stepNumber;

                    return (
                        <div key={stepNumber} className="relative flex items-center gap-2">
                            <div
                                className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold ${isActive || isPassed
                                        ? 'bg-green-600 text-white'
                                        : 'border border-gray-600 text-gray-500'
                                    }`}
                            >
                                {stepNumber}
                            </div>
                            <span
                                className={`text-sm font-medium ${isActive ? 'text-white' : 'text-gray-500'
                                    }`}
                            >
                                {step}
                            </span>
                            {isActive && (
                                <div className="absolute -bottom-[17px] left-0 h-0.5 w-full bg-green-500" />
                            )}
                        </div>
                    );
                })}
            </div>

            <div className="flex items-center gap-3">
                <button
                    type="button"
                    onClick={onSave}
                    className="rounded-md border border-white bg-transparent px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-white hover:text-black cursor-pointer"
                >
                    Lưu
                </button>
                <button
                    type="submit"
                    onClick={onContinue}
                    className="rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-green-700 cursor-pointer"
                >
                    Tiếp tục
                </button>
            </div>
        </div>
    );
};
