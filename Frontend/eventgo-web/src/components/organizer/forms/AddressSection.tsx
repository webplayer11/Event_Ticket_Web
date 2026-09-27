import React from 'react';
import { MapPin, Play } from 'lucide-react';
import { UseFormRegisterReturn } from 'react-hook-form';

interface AddressSectionProps {
    type: 'Offline' | 'Online';
    onChangeType: (type: 'Offline' | 'Online') => void;
    registers: {
        locationName?: UseFormRegisterReturn;
        province?: UseFormRegisterReturn;
        ward?: UseFormRegisterReturn;
        address?: UseFormRegisterReturn;
        onlineLink?: UseFormRegisterReturn;
    };
    errors: Record<string, any>;
    provinceValue?: string;
    charCounts: {
        locationName: number;
        address: number;
    };
}

export const AddressSection: React.FC<AddressSectionProps> = ({
    type,
    onChangeType,
    registers,
    errors,
    provinceValue,
    charCounts,
}) => {
    return (
        <div className="flex w-full flex-col gap-6 rounded-lg border border-[#2d2d2d] bg-[#12181A] p-6">
            <div className="flex flex-col gap-2">
                <label className="text-sm font-medium text-white">
                    Địa chỉ sự kiện <span className="text-green-500">*</span>
                </label>

                <div className="flex gap-2 p-1 rounded-md bg-[#1A1F24] w-fit border border-[#2d2d2d]">
                    <button
                        type="button"
                        onClick={() => onChangeType('Offline')}
                        className={`flex items-center gap-2 rounded px-4 py-1.5 text-sm font-medium transition-colors cursor-pointer ${type === 'Offline'
                                ? 'bg-green-600 text-white'
                                : 'bg-transparent text-gray-400 hover:text-white'
                            }`}
                    >
                        <MapPin className="h-4 w-4" />
                        Offline
                    </button>
                    <button
                        type="button"
                        onClick={() => onChangeType('Online')}
                        className={`flex items-center gap-2 rounded px-4 py-1.5 text-sm font-medium transition-colors cursor-pointer ${type === 'Online'
                                ? 'bg-green-600 text-white'
                                : 'bg-transparent text-gray-400 hover:text-white'
                            }`}
                    >
                        <Play className="h-4 w-4" />
                        Online
                    </button>
                </div>
            </div>

            {type === 'Offline' ? (
                <div className="flex flex-col gap-5">
                    {/* Tên địa điểm */}
                    <div className="flex flex-col gap-2">
                        <label className="text-sm text-gray-200">
                            Tên địa điểm <span className="text-green-500">*</span>
                        </label>
                        <div className="relative">
                            <input
                                type="text"
                                placeholder="Tên địa điểm"
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.locationName ? 'border-red-500' : 'border-gray-300'
                                    }`}
                                {...registers.locationName}
                            />
                            <span className={`absolute right-3 top-2.5 text-xs ${charCounts.locationName >= 80 ? 'text-red-500 font-bold' : 'text-gray-400'}`}>
                                {charCounts.locationName} / 80
                            </span>
                        </div>
                        {errors.locationName && (
                            <p className="text-xs text-red-500">{errors.locationName.message}</p>
                        )}
                    </div>

                    <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                        {/* Tỉnh/Thành */}
                        <div className="flex flex-col gap-2">
                            <label className="text-sm text-gray-200">
                                Tỉnh/Thành <span className="text-green-500">*</span>
                            </label>
                            <select
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.province ? 'border-red-500' : 'border-gray-300'
                                    }`}
                                {...registers.province}
                                defaultValue=""
                            >
                                <option value="" disabled>Tỉnh/Thành</option>
                                <option value="Hồ Chí Minh">Hồ Chí Minh</option>
                                <option value="Hà Nội">Hà Nội</option>
                                <option value="Đà Nẵng">Đà Nẵng</option>
                            </select>
                            {errors.province && (
                                <p className="text-xs text-red-500">{errors.province.message}</p>
                            )}
                        </div>

                        {/* Phường/Xã */}
                        <div className="flex flex-col gap-2">
                            <label className="text-sm text-gray-200">Phường/Xã</label>
                            <select
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${!provinceValue ? 'cursor-not-allowed bg-gray-100 text-gray-400' : ''
                                    }`}
                                {...registers.ward}
                                disabled={!provinceValue}
                                defaultValue=""
                            >
                                <option value="" disabled>Phường/Xã</option>
                                {provinceValue === 'Hồ Chí Minh' && (
                                    <>
                                        <option value="Quận 1">Quận 1</option>
                                        <option value="Quận 3">Quận 3</option>
                                    </>
                                )}
                                {provinceValue === 'Hà Nội' && (
                                    <>
                                        <option value="Hoàn Kiếm">Hoàn Kiếm</option>
                                        <option value="Ba Đình">Ba Đình</option>
                                    </>
                                )}
                            </select>
                        </div>
                    </div>

                    {/* Số nhà, đường */}
                    <div className="flex flex-col gap-2">
                        <label className="text-sm text-gray-200">
                            Số nhà, đường <span className="text-green-500">*</span>
                        </label>
                        <div className="relative">
                            <input
                                type="text"
                                placeholder="Số nhà, đường"
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.address ? 'border-red-500' : 'border-gray-300'
                                    }`}
                                {...registers.address}
                            />
                            <span className={`absolute right-3 top-2.5 text-xs ${charCounts.address >= 80 ? 'text-red-500 font-bold' : 'text-gray-400'}`}>
                                {charCounts.address} / 80
                            </span>
                        </div>
                        {errors.address && (
                            <p className="text-xs text-red-500">{errors.address.message}</p>
                        )}
                    </div>
                </div>
            ) : (
                <div className="flex flex-col gap-2">
                    <label className="text-sm text-gray-200">
                        Link tham gia sự kiện online <span className="text-green-500">*</span>
                    </label>
                    <input
                        type="text"
                        placeholder="https://zoom.us/j/..."
                        className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.onlineLink ? 'border-red-500' : 'border-gray-300'
                            }`}
                        {...registers.onlineLink}
                    />
                    {errors.onlineLink && (
                        <p className="text-xs text-red-500">{errors.onlineLink.message}</p>
                    )}
                </div>
            )}
        </div>
    );
};
