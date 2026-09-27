import React from 'react';
import { UploadCloud, X } from 'lucide-react';
import { UseFormRegisterReturn } from 'react-hook-form';

interface OrganizerInfoSectionProps {
    registers: {
        logo?: UseFormRegisterReturn;
        name?: UseFormRegisterReturn;
        info?: UseFormRegisterReturn;
    };
    errors: Record<string, any>;
    charCounts: {
        name: number;
        info: number;
    };
}

export const OrganizerInfoSection: React.FC<OrganizerInfoSectionProps> = ({
    registers,
    errors,
    charCounts,
}) => {
    const [preview, setPreview] = React.useState<string | null>(null);

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (registers.logo && registers.logo.onChange) {
            registers.logo.onChange(e);
        }
        if (file) {
            setPreview(URL.createObjectURL(file));
        }
    };

    return (
        <div className="flex w-full flex-col gap-6 rounded-lg border border-[#2d2d2d] bg-[#12181A] p-6">
            <h3 className="text-sm font-medium text-white">Thông tin Ban tổ chức</h3>

            <div className="flex flex-col gap-6 md:flex-row">
                {/* Khung upload logo */}
                <div className="flex shrink-0 flex-col gap-2">
                    <label className="text-sm text-gray-200">
                        Logo Ban tổ chức <span className="text-green-500">*</span>
                    </label>
                    <div
                        className={`relative flex h-32 w-32 cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed ${errors.logo ? 'border-red-500 bg-red-500/10' : 'border-gray-600 bg-[#1A1F24]'
                            } transition-colors hover:border-green-500 hover:bg-[#20262C]`}
                    >
                        <input
                            type="file"
                            accept="image/*"
                            className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
                            {...registers.logo}
                            onChange={handleFileChange}
                        />

                        {preview ? (
                            <div className="relative h-full w-full">
                                <img
                                    src={preview}
                                    alt="Logo"
                                    className="h-full w-full rounded-lg object-cover"
                                />
                                <button
                                    type="button"
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setPreview(null);
                                    }}
                                    className="absolute right-1 top-1 z-10 p-1 bg-black/60 text-white rounded-full hover:bg-red-500 transition-colors"
                                >
                                    <X className="h-3 w-3" />
                                </button>
                            </div>
                        ) : (
                            <div className="flex flex-col items-center text-center p-2">
                                <UploadCloud className="h-5 w-5 text-gray-400 mb-1" />
                                <span className="text-[10px] text-gray-400 leading-tight">Thêm logo ban<br />tổ chức<br />(275x275)</span>
                            </div>
                        )}
                    </div>
                    {errors.logo && <p className="text-xs text-red-500">{errors.logo.message}</p>}
                </div>

                {/* Input */}
                <div className="flex flex-1 flex-col gap-5">
                    <div className="flex flex-col gap-2">
                        <label className="text-sm text-gray-200">
                            Tên ban tổ chức <span className="text-green-500">*</span>
                        </label>
                        <div className="relative">
                            <input
                                type="text"
                                placeholder="Tên ban tổ chức"
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.name ? 'border-red-500' : 'border-gray-300'
                                    }`}
                                {...registers.name}
                            />
                            <span className={`absolute right-3 top-2.5 text-xs ${charCounts.name >= 80 ? 'text-red-500 font-bold' : 'text-gray-400'}`}>
                                {charCounts.name} / 80
                            </span>
                        </div>
                        {errors.name && <p className="text-xs text-red-500">{errors.name.message}</p>}
                    </div>

                    <div className="flex flex-col gap-2">
                        <label className="text-sm text-gray-200">
                            Thông tin ban tổ chức <span className="text-green-500">*</span>
                        </label>
                        <div className="relative">
                            <textarea
                                placeholder="Giới thiệu về ban tổ chức..."
                                rows={4}
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 resize-y ${errors.info ? 'border-red-500' : 'border-gray-300'
                                    }`}
                                {...registers.info}
                            />
                            <span className={`absolute right-3 bottom-3 text-xs ${charCounts.info >= 500 ? 'text-red-500 font-bold' : 'text-gray-400'}`}>
                                {charCounts.info} / 500
                            </span>
                        </div>
                        {errors.info && <p className="text-xs text-red-500">{errors.info.message}</p>}
                    </div>
                </div>
            </div>
        </div>
    );
};
